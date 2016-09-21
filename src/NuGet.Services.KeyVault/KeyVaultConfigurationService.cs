﻿// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace NuGet.Services.KeyVault
{
    /// <summary>
    /// Maintains a dictionary of configuration or command line arguments and injects them with secrets using an ISecretInjector on every access to refresh them.
    /// </summary>
    public class KeyVaultConfigurationService : IKeyVaultConfigurationService
    {
        private readonly ISecretInjector _secretInjector;
        private readonly IDictionary<string, string> _arguments;

        public KeyVaultConfigurationService(ISecretInjector secretInjector, IDictionary<string, string> arguments)
        {
            _secretInjector = secretInjector;
            _arguments = arguments;
        }

        /// <summary>
        /// Gets an argument and injects a secret into it.
        /// </summary>
        /// <param name="key">The key associated with the desired argument.</param>
        /// <returns>The argument associated with the given key.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the key is not found in the list of arguments.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the argument associated with the given key is null or empty.</exception>
        protected virtual async Task<string> Get(string key)
        {
            if (!_arguments.ContainsKey(key)) throw new KeyNotFoundException("Could not find key " + key + "!");

            var argumentValue = await _secretInjector.InjectAsync(_arguments[key]);
            if (string.IsNullOrEmpty(argumentValue)) throw new ArgumentNullException("Value for key " + key + " is null or empty!");

            return argumentValue;
        }

        public async Task<T> GetOrThrow<T>(string key)
        {
            var argumentString = await Get(key);

            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (converter != null)
            {
                // This will throw a NotSupportedException if no conversion is possible.
                return (T)converter.ConvertFromString(argumentString);
            }
            // If there is no converter, no conversion is possible, so throw a NotSupportedException.
            throw new NotSupportedException("No converter exists from string to " + typeof(T).Name + "!");
        }

        public async Task<T> GetOrDefault<T>(string key, T defaultValue = default(T))
        {
            try
            {
                return await GetOrThrow<T>(key);
            }
            catch (ArgumentNullException)
            {
                // The value for the specified key is null or empty.
            }
            catch (KeyNotFoundException)
            {
                // The specified key was not found in the arguments.
            }
            catch (NotSupportedException)
            {
                // Could not convert an object of type string into an object of type T.
            }
            return defaultValue;
        }

        public void Set(string key, string value)
        {
            _arguments[key] = value;
        }

        public bool ContainsKey(string key)
        {
            return _arguments.ContainsKey(key);
        }

        public async Task<bool> Contains(KeyValuePair<string, string> item)
        {
            return ContainsKey(item.Key) && item.Value.Equals(await Get(item.Key));
        }

        public void Add(KeyValuePair<string, string> item)
        {
            Add(item.Key, item.Value);
        }

        public void Add(string key, string value)
        {
            _arguments.Add(key, value);
        }

        public async Task<bool> Remove(KeyValuePair<string, string> item)
        {
            return await Contains(item) && Remove(item.Key);
        }

        public bool Remove(string key)
        {
            return ContainsKey(key) && _arguments.Remove(key);
        }

        public void Clear()
        {
            _arguments.Clear();
        }
    }
}