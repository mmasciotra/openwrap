﻿using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OpenWrap.Configuration
{
    // TODO: Move encoding and escaping into the configuration reader/writer where it belongs.
    public class RemoteRepositoryEndpoint
    {
        public RemoteRepositoryEndpoint(string configValue)
        {
            var values = ConfigurationHelper.ParseKeyValuePairs(configValue)
                .ToLookup(x=>x.Key,x=>x.Value, StringComparer.OrdinalIgnoreCase);

            if (values.Contains("token")) Token = values["token"].FirstOrDefault();
            if (values.Contains("username")) Username = values["username"].FirstOrDefault();
            if (values.Contains("password")) Password = Decrypt(values["password"].FirstOrDefault());
        }

        public RemoteRepositoryEndpoint()
        {
        }

        public string Token { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("token=\"").Append(Escape(Token)).Append("\"");
            if (Username != null) sb.Append("; username=\"").Append(Escape(Username)).Append("\"");
            if (Password != null) sb.Append("; password=\"").Append(Escape(Encrypt(Password))).Append("\"");
            return sb.ToString();
        }

        string Encrypt(string input)
        {
            if (input == null) return null;
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(input), null, DataProtectionScope.CurrentUser));
        }
        string Decrypt(string input)
        {
            if (input == null) return null;
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(input), null, DataProtectionScope.CurrentUser));
        }

        static string Escape(string input)
        {
            return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

    }
}