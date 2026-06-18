using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.DataAccess.Helpers
{
    public static class RefreshTokenHelper
    {

        public static string GenerateRawToken(int bytesLength = 64)
        {
            var bytes = RandomNumberGenerator.GetBytes(bytesLength);
            return Base64UrlEncode(bytes); // URL-safe token
        }

        // Hash the raw token so we don't store plaintext
        public static string Hash(string rawToken)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToHexString(bytes); // e.g., "A1B2C3..."
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

    }
}
