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
        public PolicyViolation(PolicyDescriptor descriptor, string message, CachedRepo repo = null, CachedUser user = null, CachedTeam team = null, IReadOnlyCollection<CachedUser> receivers = null)
        {
            Descriptor = descriptor;
            Fingerprint = ComputeFingerprint(descriptor, repo, user, team);
            Message = message;
            Repo = repo;
            User = user;
            Team = team;
            Receivers = receivers != null
                        ? receivers
                        : repo != null
                          ? repo.GetAdministrators().ToArray()
                          : team != null
                            ? team.Maintainers
                            : user != null
                              ? new[] { user }
                              : (IReadOnlyList<CachedUser>) Array.Empty<CachedUser>();
        }

        public string DiagnosticId => $"PR{((int)Descriptor) + 1:00}";
        public PolicyDescriptor Descriptor { get; }
        public Guid Fingerprint { get; }
        public string Message { get; }
        public CachedRepo Repo { get; }
        public CachedUser User { get; }
        public CachedTeam Team { get; }
        public IReadOnlyCollection<CachedUser> Receivers { get; }

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
    }
}
