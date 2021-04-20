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
                               CachedRepo? repo = null,
                               CachedSecret? secret = null,
                               CachedBranch? branch = null,
                               CachedTeam? team = null,
                               CachedUser? user = null,
                               IReadOnlyCollection<CachedUser>? assignees = null)
        {
            if (descriptor is null)
                throw new ArgumentNullException(nameof(descriptor));

            if (string.IsNullOrEmpty(title))
                throw new ArgumentException($"'{nameof(title)}' cannot be null or empty.", nameof(title));

            if (string.IsNullOrEmpty(body))
                throw new ArgumentException($"'{nameof(body)}' cannot be null or empty.", nameof(body));

            if (org is null)
                throw new ArgumentNullException(nameof(org));

            Descriptor = descriptor;
            Fingerprint = ComputeFingerprint(descriptor.DiagnosticId, repo, secret, branch, user, team);
            Title = title;
            Body = UnindentAndTrim(body);
            Org = org;
            Repo = repo;
            Secret = secret;
            Branch = branch;
            Team = team;
            User = user;
            Assignees = ComputeAssignees(org, repo, team, user, assignees);
        }

        public string DiagnosticId => Descriptor.DiagnosticId;
        public PolicyDescriptor Descriptor { get; }
        public Guid Fingerprint { get; }
        public string Title { get; }
        public CachedOrg Org { get; }
        public string Body { get; }
        public CachedRepo? Repo { get; }
        public CachedSecret? Secret { get; }
        public CachedBranch? Branch { get; }
        public CachedTeam? Team { get; }
        public CachedUser? User { get; }
        public IReadOnlyCollection<CachedUser> Assignees { get; }

        private static IReadOnlyCollection<CachedUser> ComputeAssignees(CachedOrg org,
                                                                        CachedRepo? repo,
                                                                        CachedTeam? team,
                                                                        CachedUser? user,
                                                                        IReadOnlyCollection<CachedUser>? assignees)
        {
            var result = new List<CachedUser>();

            if (assignees is not null)
                result.AddRange(assignees.Where(a => a.IsMember));

            if (result.Count == 0 && repo is not null)
                result.AddRange(repo.GetAdministrators().Where(a => a.IsMember));

            if (result.Count == 0 && team is not null)
                result.AddRange(team.GetMaintainers());

            if (result.Count == 0 && user is not null && user.IsMember)
                result.Add(user);

            if (result.Count == 0)
                result.AddRange(org.GetOwners());

            return result.ToArray();
        }

        private static Guid ComputeFingerprint(string diagnosticId,
                                               CachedRepo? repo,
                                               CachedSecret? secret,
                                               CachedBranch? branch,
                                               CachedUser? user,
                                               CachedTeam? team)
        {
            using (var fingerprintBytes = new MemoryStream())
            using (var md5 = MD5.Create())
            {
                // NOTE: If you add more pieces of information later, it is vitally important
                //       that you only add them when they aren't null. Otherwise, this will
                //       change the fingerprint of all existing violations.

                using (var writer = new StreamWriter(fingerprintBytes, Encoding.UTF8, 2048, leaveOpen: true))
                {
                    writer.WriteLine(diagnosticId);
                    writer.WriteLine(repo?.Org.Name);
                    writer.WriteLine(repo?.Name);
                    writer.WriteLine(user?.Login);
                    writer.WriteLine(team?.Name);

                    // Additional fields:

                    if (secret is not null)
                        writer.WriteLine($"secret = {secret.Name}");

                    if (branch is not null)
                        writer.WriteLine($"branch = {branch.Name}");
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
                string? line;
                while ((line = stringReader.ReadLine()) is not null)
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
                string? line;
                while ((line = stringReader.ReadLine()) is not null)
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
