# Kuro security and tool policy

Kuro includes local defensive utilities, public-data research commands, web diagnostics, and wrappers around selected open-source projects.

## Intended uses

- Inspect your own files, projects, repositories, metadata, secrets, packages, and SBOMs
- Research public domain-registration and DNS information
- Review public HTTP headers, certificates, redirects, robots files, and sitemaps
- Run passive subdomain discovery for domains you own or are authorized to assess
- Encrypt local files and generate strong passwords/passphrases

## Not included

Kuro does not ship exploit automation, credential attacks, malware, persistence, stealth tooling, access-control bypasses, or a broad automatic port scanner.

## Open-source downloads

The tool manager downloads matching Windows x64 assets from the official GitHub repository configured for each project. Kuro records the downloaded asset name, source, version, license, and SHA-256 digest in `%LOCALAPPDATA%\Kuro\Tools\<tool>\kuro-tool.json`. ExifTool is installed through WinGet.

Review each upstream project's license and documentation before redistribution. Third-party tools remain the work of their respective authors and are not owned by Kuro or @Lthest.
