# Kuro automatic updates — complete setup

This uses **Velopack + GitHub Releases**. You decide when a version is published.
Normal Kuro users cannot publish updates.

## Important before starting

The `Kuro-Setup.exe` you built locally as version **1.1.2** did not contain a real
GitHub repository URL, so that particular build cannot discover online updates.

Your first updater-enabled release should be **1.1.3**.

Anyone who installed the old 1.1.2 build must manually run the new 1.1.3 setup
one time. After that, versions 1.1.4, 1.1.5, and later can update automatically.

## Part 1 — Create the GitHub repository

1. Sign in to GitHub.
2. Create a new repository.
3. Use the repository name you entered in the setup script.
4. Keep the repository **Public** for the easiest updater setup.
5. Do not initialize it with a README if your local Kuro folder already has files.

A public repository is easiest because installed copies of Kuro can read its
GitHub Releases without storing a private access token inside the program.

## Part 2 — Upload the complete project

The repository root must look like this:

```text
Kuro.sln
Kuro/
  Kuro.csproj
  AutoUpdater.cs
  App.xaml.cs
  ...
.github/
  workflows/
    publish-kuro.yml
AUTO-UPDATER-GUIDE.md
```

Do not upload only `bin`, `Debug`, `Release`, `publish`, or `DIST-TO-SHARE`.
Upload the source project folder containing `Kuro.sln`.

### Easiest upload method: GitHub Desktop

1. Install and open GitHub Desktop.
2. Choose **File → Add local repository**.
3. Select the folder containing `Kuro.sln`.
4. If it says the folder is not a repository, choose **create a repository here**.
5. Commit all files with a message such as `Initial Kuro updater setup`.
6. Click **Publish repository**.
7. Make sure the repository name matches the one you entered.
8. Make sure **Keep this code private** is unchecked.
9. Publish it.

## Part 3 — Allow the workflow to create releases

In the GitHub repository:

1. Open **Settings**.
2. Open **Actions → General**.
3. Find **Workflow permissions**.
4. Select **Read and write permissions**.
5. Click **Save**.

The workflow also contains `permissions: contents: write`, but enabling the
repository setting avoids release-permission failures.

## Part 4 — Publish the first updater-enabled installer

1. Open the repository's **Actions** tab.
2. Select **Publish Kuro Update**.
3. Click **Run workflow**.
4. Enter version:

```text
1.1.3
```

5. Enter release notes, for example:

```text
Enabled automatic updates through GitHub Releases.
```

6. Click the green **Run workflow** button.
7. Wait for the workflow to finish with a green check.
8. Open the repository's **Releases** section.
9. Open **Kuro 1.1.3**.
10. Download the generated setup executable.
11. Install that setup on your own PC.
12. Send that setup executable to users.

That installed 1.1.3 copy now knows which GitHub repository to check.

## Part 5 — Push every future update

Example: you change Kuro and want to release 1.1.4.

1. Save and test the changes in Visual Studio.
2. Commit the changed project files in GitHub Desktop.
3. Click **Push origin**.
4. Open GitHub → **Actions → Publish Kuro Update**.
5. Click **Run workflow**.
6. Enter:

```text
1.1.4
```

7. Add notes describing the changes.
8. Run the workflow.

The workflow builds Kuro, downloads the previous Velopack release information,
creates the new installer/update packages, and publishes a GitHub Release.

When a user next opens an installed older copy, Kuro checks the repository,
downloads the newer package, applies it, and restarts.

## Version rule

Every published version must be higher than the previous version:

```text
1.1.3
1.1.4
1.1.5
1.2.0
```

Never publish a second, different update using a version that already exists.

## Testing the updater

1. Install Kuro 1.1.3 from GitHub Releases.
2. Make an obvious change, such as altering the startup text.
3. Commit and push the change.
4. Publish version 1.1.4 through the workflow.
5. Wait for the GitHub Release to appear.
6. Open the installed 1.1.3 Kuro shortcut.

Do not test auto-updating by pressing F5 in Visual Studio. Velopack intentionally
updates installed copies, not the loose developer build.

## Files in a GitHub release

Keep all generated Velopack assets. In particular, do not delete the release
feed JSON or package files. Kuro needs them to discover and download updates.

## If the workflow fails

Open the failed workflow run, select the red step, and copy the full error.
Common causes are:

- The repository workflow permission is read-only.
- The entered version already exists.
- The `.github/workflows/publish-kuro.yml` file was not uploaded.
- Only the compiled `bin` folder was uploaded instead of the source project.
- The project is not at `Kuro/Kuro.csproj`.

## Keeping the source private later

The simple setup uses a public repository. A private GitHub repository requires
authentication for update downloads, and embedding your private GitHub token in
Kuro would be unsafe.

A more advanced arrangement is:

- private repository for source code;
- separate public repository containing only installer/update releases.

Set up the public single-repository version first so the updater works.
