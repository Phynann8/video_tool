using System;
using System.Security.Cryptography;
using System.Text;

namespace Start.Infrastructure.Services
{
    public static class DramaboxCrypto
    {
        private static readonly string _privateKeyPem;

        static DramaboxCrypto()
        {
            _privateKeyPem = InitializePrivateKey();
        }

        private static string DecodeString(string str)
        {
            var result = new StringBuilder();
            foreach (char c in str)
            {
                int code = (int)c;
                if (code >= 33 && code <= 126)
                {
                    code -= 20;
                    if (code < 33) code += 126 - 33 + 1; // Corrected offset logic for C#
                }
                result.Append((char)code);
            }
            return result.ToString();
        }

        private static string InitializePrivateKey()
        {
            const string part1 = "MIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQC9Q4Y5QX5j08HrnbY3irfKdkEllAU2OORnAjlXDyCzcm2Z6ZRrGvtTZUAMelfU5PWS6XGEm3d4kJEKbXi4Crl8o2E/E3YJPk1lQD1d0JTdrvZleETN1ViHZFSQwS3L94Woh0E3TPebaEYq88eExvKu1tDdjSoFjBbgMezySnas5Nc2xF28";
            
            // The obfuscated part2 from hndko/dramabox-rest-api-node
            string part2Encoded = @"l|d,WL$EI,?xyw+*)^#?U`[whXlG`-GZif,.jCxbKkaY""{w*y]_jax^/1iVDdyg(Wbz+z/$xVjCiH0lZf/d|%gZglW)""~J,^~}w""}m(E'eEunz)eyEy`XGaVF|_(Kw)|awUG""'{{e#%$0E.ffHVU++$giHzdvC0ZLXG|U{aVUUYW{{YVU^x),J'If`nG|C[`ZF),xLv(-H'}ZIEyCfke0dZ%aU[V)""V0}mhKvZ]Gw%-^a|m'`\f}{(~kzi&zjG+|fXX0$IH#j`+hfnME""|fa/{.j.xf,""LZ.K^bZy%c.W^/v{x#(J},Ua,ew#.##K(ki)$LX{a-1\MG/zL&JlEKEw'Hg|D&{EfuKYM[nGKx1V#lFu^V_LjVzw+n%+,Xd";
            
            // Ported DecodeString logic
            string part2 = PortedDecodeString(part2Encoded);
            
            const string part3 = "x52e71nafqfbjXxZuEtpu92oJd6A9mWbd0BZTk72ZHUmDcKcqjfcEH19SWOphMJFYkxU5FRoIEr3/zisyTO4Mt33ZmwELOrY9PdlyAAyed7ZoH+hlTr7c025QROvb2LmqgRiUT56tMECgYEA+jH5m6iMRK6XjiBhSUnlr3DzRybwlQrtIj5sZprWe2my5uYHG3jbViYIO7GtQvMTnDrBCxNhuM6dPrL0cRnbsp/iBMXe3pyjT/aWveBkn4R+UpBsnbtDn28r1MZpCDtr5UNc0TPj4KFJvjnV/e8oGoyYEroECqcw1LqNOGDiLhkCgYEAwaemNePYrXW+MVX/hatfLQ96tpxwf7yuHdENZ2q5AFw73GJWYvC8VY+TcoKPAmeoCUMltI3TrS6K5Q/GoLd5K2BsoJrSxQNQFd3ehWAtdOuPDvQ5rn/2fsvgvc3rOvJh7uNnwEZCI/45WQg+UFWref4PPc+ArNtp9Xj2y7LndwkCgYARojIQeXmhYZjG6JtSugWZLuHGkwUDzChYcIPd";
            const string part4 = "W25ndluokG/RzNvQn4+W/XfTryQjr7RpXm1VxCIrCBvYWNU2KrSYV4XUtL+B5ERNj6In6AOrOAifuVITy5cQQQeoD+AT4YKKMBkQfO2gnZzqb8+ox130e+3K/mufoqJPZeyrCQKBgC2fobjwhQvYwYY+DIUharri+rYrBRYTDbJYnh/PNOaw1CmHwXJt5PEDcml3+NlIMn58I1X2U/hpDrAIl3MlxpZBkVYFI8LmlOeR7ereTddN59ZOE4jY/OnCfqA480Jf+FKfoMHby5lPO5OOLaAfjtae1FhrmpUe3EfIx9wVuhKBAoGBAPFzHKQZbGhkqmyPW2ctTEIWLdUHyO37fm8dj1WjN4wjRAI4ohNiKQJRh3QE11E1PzBTl9lZVWT8QtEsSjnrA/tpGr378fcUT7WGBgTmBRaAnv1P1n/Tp0TSvh5XpIhhMuxcitIgrhYMIG3GbP9JNAarxO/qPW6Gi0xWaF7il7Or";

            return $"-----BEGIN PRIVATE KEY-----\n{part1}{part2}{part3}{part4}\n-----END PRIVATE KEY-----";
        }

        private static string PortedDecodeString(string str)
        {
            var result = new StringBuilder();
            foreach (char c in str)
            {
                int code = (int)c;
                if (code >= 33 && code <= 126)
                {
                    code -= 20;
                    if (code < 33) code += (126 - 33); // Match JS exactly: 126 - 33 = 93
                }
                result.Append((char)code);
            }
            return result.ToString();
        }

        public static string Sign(string data)
        {
            using var rsa = RSA.Create();
            try 
            {
                // Ensure no whitespace in the reconstructed base64
                string base64 = _privateKeyPem
                    .Replace("-----BEGIN PRIVATE KEY-----", "")
                    .Replace("-----END PRIVATE KEY-----", "")
                    .Replace("\n", "")
                    .Replace("\r", "")
                    .Trim();
                
                rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(base64), out _);
            }
            catch
            {
                // Fallback to ImportFromPem if ImportPkcs8PrivateKey fails
                rsa.ImportFromPem(_privateKeyPem);
            }
            
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            byte[] signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            
            return Convert.ToBase64String(signatureBytes);
        }
        
        public static string GetRandomAndroidId()
        {
            byte[] bytes = new byte[4];
            RandomNumberGenerator.Fill(bytes);
            return "ffffffff" + BitConverter.ToString(bytes).Replace("-", "").ToLower() + "000000000";
        }

        public static string GenerateUUID()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
