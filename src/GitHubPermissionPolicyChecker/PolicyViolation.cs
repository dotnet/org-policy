using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Terrajobst.GitHubCaching;

namespace GitHubPermissionPolicyChecker
{
    internal sealed class PolicyViolation
    {
        public PolicyViolation(PolicyDescriptor descriptor,
                               string title,
                               string body,
                               CachedRepo repo = null,
                               CachedUser user = null,
                               CachedTeam team = null,
                               IReadOnlyCollection<CachedUser> assignees = null)
        {
            Descriptor = descriptor;
            Fingerprint = ComputeFingerprint(descriptor, repo, user, team);
            Title = title;
            Body = UnindentAndTrim(body);
            Repo = repo;
            User = user;
            Team = team;
            Assignees = assignees != null
                        ? assignees
                        : repo != null
                          ? repo.GetAdministrators().ToArray()
                          : team != null
                            ? team.Maintainers
                            : user != null
                              ? new[] { user }
                              : (IReadOnlyList<CachedUser>)Array.Empty<CachedUser>();
        }

        public string DiagnosticId => $"PR{((int)Descriptor) + 1:00}";
        public PolicyDescriptor Descriptor { get; }
        public Guid Fingerprint { get; }
        public string Title { get; }
        public string Body { get; }
        public CachedRepo Repo { get; }
        public CachedUser User { get; }
        public CachedTeam Team { get; }
        public IReadOnlyCollection<CachedUser> Assignees { get; }

        private static Guid ComputeFingerprint(PolicyDescriptor descriptor, CachedRepo repo, CachedUser user, CachedTeam team)
        {
            using (var fingerprintBytes = new MemoryStream())
            using (var md5 = MD5.Create())
            {
                using (var writer = new StreamWriter(fingerprintBytes, Encoding.UTF8, 2048, leaveOpen: true))
                {
                    // NOTE: We want to be able to rename the enum without breaking fingerprinting.
                    //       That's why we'll just use the enum's integer, rather than the numeric value.

                    var ruleNumber = (int)descriptor;
                    writer.WriteLine(ruleNumber);
                    writer.WriteLine(repo?.Org.Name);
                    writer.WriteLine(repo?.Name);
                    writer.WriteLine(user?.Login);
                    writer.WriteLine(team?.Name);
                }

                fingerprintBytes.Position = 0;

                var hashBytes = md5.ComputeHash(fingerprintBytes);
                return new Guid(hashBytes);
            }
        }

        private static string UnindentAndTrim(string text)
        {
            return Unindent(text).Trim();
        }

        public static string Unindent(string text)
        {
            var minIndent = int.MaxValue;

            using (var stringReader = new StringReader(text))
            {
                string line;
                while ((line = stringReader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var indent = line.Length - line.TrimStart().Length;
                    minIndent = Math.Min(minIndent, indent);
                }
            }

            var sb = new StringBuilder();
            using (var stringReader = new StringReader(text))
            {
                string line;
                while ((line = stringReader.ReadLine()) != null)
                {
                    var unindentedLine = line.Length < minIndent
                        ? line
                        : line.Substring(minIndent);
                    sb.AppendLine(unindentedLine);
                }
            }

            return sb.ToString();
        }
    }
}
