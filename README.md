# Policy tooling for the dotnet org 

[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet.org-policy?branchName=master)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=650&branchName=master)

This repo contains tools and tracks policy violations.

## Policies and process

For details on policies, see [the docs](doc/README.md).

## Usage

For the `dotnet` org, the policies are evaluated daily and violations are posted
in the internal repo [org-policy-violations]. The repo is internal because it
contains names of private repos and teams.

[org-policy-violations]: https://github.com/dotnet/org-policy-violations

## Running locally

You can run the tool locally by cloning this repo running `policop.cmd` from the
root.

### Getting the org data

Before you can do anything useful, you need to get access to the org data, which
includes repos, teams, users and their relationships. This also includes access
to linking information between Microsoft user accounts and GitHub user accounts.

Due to performance and API rate limitations it's not practical to query this
information from GitHub when you're experimenting and trying to analyze the org.
So instead, you can download a cached version of the org that was computed and
uploaded to a private Azure DevOps project during the nightly policy runs.

You do this by running:

```PS
$ .\policop cache-build
```

This will download the latest version of the org data and store it on your local
machine. If you run this command for the first time, it will take you to a
website where you'll need to create an access token that the tool will then
store and use on future calls.

You can check how old your local cache is by running

```PS
$ .\policop cache-info
```

You can also clear the cache with

```PS
$ .\policop cache-clear -f
```

### Evaluating policies

In order to check policies, you simply use this command:

```PS
$ .\policop check --excel
```

This will compute all policy violations and display the result in Excel. You can
also write them to a file if you prefer that:

```PS
$ .\policop check -o D:\temp\test.csv
```

### Querying org data

The primary command is `policop list` which you can use to query information
from the org.

Using `-r`, `-t`, and `-u` you can list all components of the org:

* `-r` the list of repos
* `-t` the list of teams
* `-u` the list of users
* `-r -t` the list of repos and permissions teams are given
* `-r -u` the list of repos and permissions users are given
* `-t -u` the list of teams and their members
* `-r -t -u` the list of repos and permissions teams & users are given

Each of those options accept a list of terms you can use to filter,
with basic wild card support, such as `*core*` or `dotnet*`.

So to list all teams whose name contains the text `core` you'd do this:

```PS
$ .\policop list -t *core*
```

To find all members of all teams named `*core*` you'd do this:

```PS
# List team members of teams whose name contains "core"
$ .\policop list -t *core* -u 
```

Using `-f` you can also filter:

```PS
# List all repos whose name contains dotnet and where a team
# grants admin access
$ .\policop list -r *dotnet* -t -f rt:permission=admin
```

For columns returning `Yes`/`No` you can also use the simple
version:

```PS
# List all private repos
$ .\policop list -r -f r:private
```

And lastly, using `-c` you can create custom reports with specific columns:

```PS
# List all private repos and show their name, description and list of admins
$ .\policop list -r -f r:private -c r:name r:description r:admins
```

The available columns can be listed by running

```PS
$ .\policop list-columns
```

The naming convention indicates when the columns can be used:

* `r:*` when repos are included
* `t:*` when teams are included
* `u:*` when users are included
* `rt:*` when repos and teams are included
* `ru:*` when repos and users are included
* `tu:*` when teams and users are included
* `rtu:*` when repos, teams, and users are included

In general, `policop list` will print the results to the console but with `-o`
you can write to a file and with `--excel` you can send it straight into Excel.
