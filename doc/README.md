# Policy rules

Rule            | Severity | Title
----------------|----------|------------------------------------------------
[PR01](PR01.md) | Error    | Microsoft employees should be linked
[PR02](PR02.md) | Error    | Microsoft-owned repo should only grant 'pull' to externals
[PR03](PR03.md) | Error    | Microsoft-owned team should only contain Microsoft users
[PR04](PR04.md) | Error    | Team should be owned by Microsoft
[PR05](PR05.md) | Error    | The team 'Microsoft' should only grant 'pull' access
[PR06](PR06.md) | Warning  | Inactive repos should be archived
[PR07](PR07.md) | Warning  | Unused team should be removed
[PR08](PR08.md) | Error    | Too many repo admins
[PR09](PR09.md) | Error    | Team should be owned by Microsoft
[PR10](PR10.md) | Error    | Admins should be in teams
[PR11](PR11.md) | Warning  | Repos should have a sufficient number of admins
[PR12](PR12.md) | Warning  | Bots should be in the 'bots' team

## Process

This tool runs automatically and will file policy issues in here, labeled with
`area-violation` and with the particular policy (e.g. `PR01`) and assign it to
the appropriate user:

* For org issues, it will assign the org owners.
* For repo issues, it will assign the repo admins. If there are no admins, it
  will assign the org owners.
* For team issues, it will assign the maintainers. If there are no maintainers,
  it will assign the org owners.
* For issues with a specific user account, it will assign the user.

If an issue already exists but was close, it will reopen the issue. If the
assignees believe the policy violation is necessary, they should loop in the
[@policy-cops] team.

When consensus is reached that the violation is acceptable, the [@policy-cops]
will label the issue `policy-override` and close it. The tool will not reopen
such issues.

## Policy modification

If you think new polices are needed or policies should be changed, file an
issue.
