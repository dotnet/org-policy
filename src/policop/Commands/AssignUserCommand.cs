using Microsoft.DotnetOrg.GitHubCaching;

using Mono.Options;

using Octokit;

namespace Microsoft.DotnetOrg.PolicyCop.Commands;

internal sealed class AssignUserCommand : ToolCommand
{
    private string? _orgName;
    private string? _userName;
    private string? _repoName;
    private string? _teamName;
    private string? _permission;
    private bool _unassign;

    public override string Name => "assign-user";

    public override string Description => "Assigns or unassigns a user to a repo or team";

    public override void AddOptions(OptionSet options)
    {
        options.AddOrg(v => _orgName = v)
               .Add("u=", "Specifies the user", v => _userName = v)
               .Add("r=", "Specifies the repo", v => _repoName = v)
               .Add("t=", "Specifies the team", v => _teamName = v)
               .Add("p=", "Sets the {permission} (default: read)", v => _permission = v)
               .Add("d", "Unassigns the user or team", v => _unassign = true);
    }

    public override async Task ExecuteAsync()
    {
        if (string.IsNullOrEmpty(_orgName))
        {
            Console.Error.WriteLine($"error: --org must be specified");
            return;
        }

        if (string.IsNullOrEmpty(_userName))
        {
            Console.Error.WriteLine($"error: -u must be specified");
            return;
        }

        if (!string.IsNullOrEmpty(_repoName) && !string.IsNullOrEmpty(_teamName))
        {
            Console.Error.WriteLine($"error: cannot specify both -r and -t");
            return;
        }

        if (string.IsNullOrEmpty(_repoName) && string.IsNullOrEmpty(_teamName))
        {
            Console.Error.WriteLine($"error: either -r or -t must be specified");
            return;
        }

        string permission;

        switch (_permission)
        {
            case null:
            case "read":
                permission = "pull";
                break;
            case "write":
                permission = "push";
                break;
            case "admin":
                permission = "admin";
                break;
            default:
                Console.Error.WriteLine($"error: permission can be 'read', 'write', or 'admin' but not '{_permission}'");
                return;
        }

        var client = await GitHubClientFactory.CreateAsync();

        User user;

        try
        {
            user = await client.User.Get(_userName);
        }
        catch (Exception)
        {
            Console.Error.WriteLine($"error: user '{_userName}' doesn't exist");
            return;
        }

        if (!string.IsNullOrEmpty(_teamName))
        {
            var teams = await client.Organization.Team.GetAll(_orgName);

            var team = teams.SingleOrDefault(t => string.Equals(t.Name, _teamName, StringComparison.OrdinalIgnoreCase) ||
                                                  string.Equals(t.Slug, _teamName, StringComparison.OrdinalIgnoreCase));

            if (team is null)
            {
                Console.Error.WriteLine($"error: team '{_teamName}' doesn't exist");
                return;
            }

            if (_unassign)
            {
                await client.Organization.Team.RemoveMembership(team.Id, _userName);
                Console.Out.WriteLine($"Removed user '{_userName}' from team '{_teamName}'");
            }
            else
            {
                await client.Organization.Team.AddOrEditMembership(team.Id, _userName, new UpdateTeamMembership(TeamRole.Member));
                Console.Out.WriteLine($"Added user '{_userName}' to team '{_teamName}'");
            }
        }
        else if (!string.IsNullOrEmpty(_repoName))
        {
            Repository? repo;
            try
            {
                repo = await client.Repository.Get(_orgName, _repoName);
            }
            catch (Exception)
            {
                repo = null;
            }

            if (repo is null)
            {
                Console.Error.WriteLine($"error: repo '{_orgName}/{_repoName}' doesn't exist");
                return;
            }

            if (_unassign)
            {
                await client.Repository.Collaborator.Delete(_orgName, _repoName, _userName);
                Console.Out.WriteLine($"Removed user '{_userName}' from repo '{_repoName}'");
            }
            else
            {
                await client.Repository.Collaborator.Add(_orgName, _repoName, _userName, new CollaboratorRequest(permission));
                Console.Out.WriteLine($"Added user '{_userName}' to repo '{_repoName}'");
            }
        }
    }
}