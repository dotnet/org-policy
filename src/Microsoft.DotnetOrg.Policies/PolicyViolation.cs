using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.Policies
{
    public sealed class PolicyViolation
    {
        public PolicyViolation(PolicyDescriptor descriptor,
                               string title,
                               string body,
                               CachedOrg org,
                               CachedRepo repo = null,
                               CachedTeam team = null,
                               CachedUser user = null,
                               IReadOnlyCollection<CachedUser> assignees = null)
        {
            Descriptor = descriptor;
            Fingerprint = ComputeFingerprint(descriptor.DiagnosticId, repo, user, team);
            Title = title;
            Body = UnindentAndTrim(body);
            Org = org;
            Repo = repo;
            Team = team;
            User = user;
            Assignees = ComputeAssignees(org, repo, team, user, assignees);
        }

        public string DiagnosticId { get; }
        public PolicyDescriptor Descriptor { get; }
        public Guid Fingerprint { get; }
        public string Title { get; }
        public CachedOrg Org { get; }
        public string Body { get; }
        public CachedRepo Repo { get; }
        public CachedTeam Team { get; }
        public CachedUser User { get; }
        public IReadOnlyCollection<CachedUser> Assignees { get; }

        private static IReadOnlyCollection<CachedUser> ComputeAssignees(CachedOrg org, CachedRepo repo, CachedTeam team, CachedUser user, IReadOnlyCollection<CachedUser> assignees)
        {
            if (assignees != null && assignees.Count > 0)
                return assignees;

            if (repo != null)
                return repo.GetAdministrators().ToArray();

            if (team != null)
                return team.GetMaintainers().ToArray();

            if (user != null)
                return new[] { user };

            return org.GetOwners().ToArray();
        }

        private static Guid ComputeFingerprint(string diagnosticId, CachedRepo repo, CachedUser user, CachedTeam team)
        {
            using (var fingerprintBytes = new MemoryStream())
            using (var md5 = MD5.Create())
            {
                using (var writer = new StreamWriter(fingerprintBytes, Encoding.UTF8, 2048, leaveOpen: true))
                {
                    writer.WriteLine(diagnosticId);
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
