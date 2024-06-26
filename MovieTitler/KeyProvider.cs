﻿using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using MovieTitler.Interfaces;

namespace MovieTitler
{
    /// <summary>
    /// Provides access to an encryption key in Azure Key Vault. This key is
    /// used as the signing key for the ActivityPub actor.
    /// </summary>
    public class KeyProvider(string VaultUri) : IActorKeyProvider
    {
        private record PublicKey(string Pem) : IActorKey;

        /// <summary>
        /// Retrieves the public key and renders it in PEM format for use in the ActivityPub actor object.
        /// </summary>
        /// <returns>An object that contains the public key in PEM format</returns>
        public async Task<IActorKey> GetPublicKeyAsync()
        {
            var tokenCredential = new DefaultAzureCredential();
            var keyClient = new KeyClient(new Uri(VaultUri), tokenCredential);
            var key = await keyClient.GetKeyAsync("bot");
            byte[] arr = key.Value.Key.ToRSA().ExportSubjectPublicKeyInfo();
            string str = Convert.ToBase64String(arr);
            return new PublicKey($"-----BEGIN PUBLIC KEY-----\n{str}\n-----END PUBLIC KEY-----");
        }

        /// <summary>
        /// Creates a signature for the given data usign the private key.
        /// </summary>
        /// <param name="data">The data to sign</param>
        /// <returns>An RSA SHA-256 signature</returns>
        public async Task<byte[]> SignRsaSha256Async(byte[] data)
        {
            var tokenCredential = new DefaultAzureCredential();
            var keyClient = new KeyClient(new Uri(VaultUri), tokenCredential);
            var cryptographyClient = keyClient.GetCryptographyClient("bot");
            var result = await cryptographyClient.SignDataAsync(SignatureAlgorithm.RS256, data);
            return result.Signature;
        }
    }
}
