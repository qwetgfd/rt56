using CryptHash.Net.Encryption.AES.AE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Common.Crypto
{
    public class Crypto
    {
        public static string Decrypt(string encryptedString, string encryptionKey)
        {
            var aes256 = new AE_AES_256_CBC_HMAC_SHA_512();
            return aes256.DecryptString(encryptedString, encryptionKey, true).DecryptedDataString;
        }

        public static string Encrypt(string data, string encryptionKey)
        {
            var aes256 = new AE_AES_256_CBC_HMAC_SHA_512();
            return aes256.EncryptString(data, encryptionKey, true).EncryptedDataBase64String;
        }

        public static byte[] ComputeHash(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            }
        }
    }
}
