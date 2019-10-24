# GitHub Permission Surveyor

This tool helps to audit GitHub organizations by producing a report like this:

| repo         | repo-state | repo-last-pushed | principal-kind | principal             | permission | via-team                   |
|--------------|------------|------------------|----------------|-----------------------|------------|----------------------------|
| Some repo    | public     | 10/23/2019 8:30  | team           | Some Team             | admin      | Some Team                  |
| Some repo    | public     | 10/23/2019 8:30  | team           | Another Team          | push       | Another Team               |
| Some repo    | public     | 10/23/2019 8:30  | user           | Some Owner            | admin      | (Owner)                    |
| Some repo    | public     | 10/23/2019 8:30  | user           | Some User             | push       | Some Team\Some Nested Team |
| Another repo | public     | 10/23/2019 3:30  | user           | Another Owner         | admin      | (Owner)                    |
| Another repo | public     | 10/23/2019 3:30  | user           | Some User             | push       | Some Team                  |
| Another repo | public     | 10/23/2019 3:30  | user           | Some External User    | pull       | (Collaborator)             |
| Another repo | public     | 10/23/2019 3:30  | user           | Another External User | push       | (Collaborator)             |

## Usage

It's a command line app. It will prompt for log in information on first run
so it can create a personal access token (PAT). It needs read-only access to
repos and orgs.

```
$ \.GitHubPermissionSurveyor.exe <org-name> [output-file]
```

If you don't specify an output file, the app will show the results in Excel. In
case you don't have Excel, you'll get an error before the tool runs and you need
to specify a path for the `.csv` file with the report.

```
$ .\GitHubPermissionSurveyor.exe dotnet C:\work\permissions.csv
```
