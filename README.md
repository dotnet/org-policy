# Policy tooling for the dotnet org 

This repo contains tools and tracks policy violations.

## `policop`

This tool helps with enforcing [the policies] we're using in this organization.

[the policies]: doc/README.md

```
usage: policop --org <org> [OPTIONS]+
      --org=name             The name of the GitHub organization
  -o, --output=path          The path where the output .csv file should be written to.
      --cache-location=path  The path where the .json cache should be written to.
      --github-token=token   The GitHub API token to be used.
      --ospo-token=token     The OSPO API token to be used.
      --policy-repo=repo     The GitHub repo policy violations should be file in.
  @file                      Read response file for more options.
```

Example:

```
$ .\policop.exe --org dotnet -o D:\violations.csv
```

## `permaudit`

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

```
usage: permaudit --org <org> [OPTIONS]+
      --org=name             The name of the GitHub organization
  -o, --output=path          The path where the output .csv file should be written to.
      --cache-location=path  The path where the .json cache should be written to.
  @file                      Read response file for more options.
```

`permaudit` will prompt for log in information on first run so it can create a
personal access token (PAT). It needs read-only access to repos and orgs.

```
$ .\permaudit.exe --org <org-name> [-o <output-file>]
```

If you don't specify an output file, the app will show the results in Excel. In
case you don't have Excel, you'll get an error before the tool runs and you need
to specify a path for the `.csv` file with the report.

```
$ .\permaudit.exe --org dotnet -o C:\work\permissions.csv
```
