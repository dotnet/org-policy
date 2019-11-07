# Policy tooling for the dotnet org 

[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet.org-policy?branchName=master)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=650&branchName=master)

This repo contains tools and tracks policy violations.

## Policies and process

For details on policies, see [the docs](doc/README.md).

## `policop check`

This tool helps with enforcing [the policies] we're using in this organization.

[the policies]: doc/README.md

```
usage: policop check [OPTIONS]+
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
$ .\policop check --org dotnet -o D:\violations.csv
```

## `policop audit`

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
usage: policop audit [OPTIONS]+
      --org=name             The name of the GitHub organization
  -o, --output=path          The path where the output .csv file should be written to.
      --cache-location=path  The path where the .json cache should be written to.
      --excel                Shows the results in Excel
  @file                      Read response file for more options.
```

`policop audit` will prompt for log in information on first run so it can create a
personal access token (PAT). It needs read-only access to repos and orgs.

```
$ .\policop audit --org <org-name> [-o <output-file>]
```

If you don't specify an output file, the app will print the results to the
console. Alternatively, you can show the results in Excel by specifying
`--excel`.

```
$ .\policop audit --org dotnet -o C:\work\permissions.csv
```
