# Kuro Built-in Command Reference

Kuro 1.1.1 contains **234** registered built-in commands. Aliases may provide additional names.

## Archives

| Command | Usage | Description |
|---|---|---|
| `backup` | `backup <path>` | Create a timestamped file copy or folder ZIP backup. |
| `unzip` | `unzip <archive.zip> [destination]` | Safely extract a ZIP archive. |
| `zip` | `zip <archive.zip> <path...>` | Create a ZIP archive from files and folders. |
| `ziplist` | `ziplist <archive.zip>` | List files inside a ZIP archive. |

## Customize

| Command | Usage | Description |
|---|---|---|
| `bookmark` | `bookmark [list\|add\|go\|remove]` | Save and revisit directory shortcuts. |
| `config` | `config [show\|path\|reset]` | Inspect or reset your saved settings. |
| `font` | `font [10-30]` | Change the terminal font size. |
| `greeting` | `greeting [on\|off]` | Show or hide the startup greeting. |
| `motd` | `motd [show\|set <text>\|clear\|reset]` | Customize the startup message. |
| `note` | `note [list\|add\|show\|remove\|clear]` | Store quick persistent notes. |
| `opacity` | `opacity [0.20-0.96]` | Change terminal background transparency. |
| `palette` | `palette` | Show the active theme's color palette. |
| `prompt` | `prompt [show\|set <format>\|reset]` | Customize the saved prompt format. |
| `theme` | `theme [name\|list]` | Change the saved color theme. |
| `title` | `title [show\|set <text>\|reset]` | Customize the title-bar name. |

## Defensive

| Command | Usage | Description |
|---|---|---|
| `certfile` | `certfile <certificate-file>` | Inspect a local X.509 certificate file. |
| `defang` | `defang <indicator>` | Defang URLs, domains, and IP indicators for safe sharing. |
| `entropy` | `entropy <text>` | Calculate Shannon entropy for local text. |
| `hashid` | `hashid <hash>` | Identify likely hash algorithms by format and length. |
| `ioc` | `ioc <value>` | Classify a possible URL, IP, domain, email, or hash indicator. |
| `passphrase` | `passphrase [words]` | Generate a random local passphrase. |
| `password` | `password [length]` | Generate a cryptographically random local password. |
| `passwordcheck` | `passwordcheck <password>` | Estimate password strength locally without sending it anywhere. |
| `randombytes` | `randombytes [count]` | Generate cryptographically random bytes as hexadecimal. |
| `refang` | `refang <indicator>` | Reverse common indicator defanging. |

## Developer

| Command | Usage | Description |
|---|---|---|
| `binary` | `binary <text>` | Convert UTF-8 text to binary bytes. |
| `bytes` | `bytes <number>` | Format a byte count into readable units. |
| `case` | `case <camel\|pascal\|snake\|kebab\|title> <text>` | Convert text between common naming styles. |
| `csvinfo` | `csvinfo <file>` | Show basic CSV row and column information. |
| `escape` | `escape <text>` | Escape control characters for C#-style display. |
| `guid` | `guid` | Generate a new GUID. |
| `htmldecode` | `htmldecode <text>` | HTML-decode text. |
| `htmlencode` | `htmlencode <text>` | HTML-encode text. |
| `jsonget` | `jsonget <file> <dot.path>` | Read a value from a JSON file using a simple dot path. |
| `jsonkeys` | `jsonkeys <file>` | List top-level JSON object keys. |
| `jsontext` | `jsontext <json>` | Validate and pretty-print inline JSON. |
| `jwt-decode` | `jwt-decode <token>` | Decode JWT header and payload without verifying the signature. |
| `lorem` | `lorem [word-count]` | Generate placeholder text. |
| `morse` | `morse <text>` | Encode supported text as Morse code. |
| `regex` | `regex <pattern> <text>` | Test a regular expression with a safety timeout. |
| `regexreplace` | `regexreplace <pattern> <replacement> <text>` | Replace regex matches with a safety timeout. |
| `rot13` | `rot13 <text>` | Apply the reversible ROT13 transformation. |
| `semver` | `semver <version1> <version2>` | Compare two dotted version numbers. |
| `slug` | `slug <text>` | Create a lowercase URL-friendly slug. |
| `timestamp` | `timestamp [unix-seconds]` | Show or convert a Unix timestamp. |
| `unbinary` | `unbinary <bits>` | Convert binary bytes back to UTF-8 text. |
| `unescape` | `unescape <text>` | Unescape common backslash sequences. |
| `unix` | `unix [date/time]` | Convert a local date/time to Unix seconds. |
| `unmorse` | `unmorse <code>` | Decode Morse code separated by spaces and slashes. |
| `urldecode` | `urldecode <text>` | Decode percent-encoded URL text. |
| `urlencode` | `urlencode <text>` | Percent-encode text for a URL component. |
| `urlparse` | `urlparse <url>` | Break a URL into its components. |
| `xml` | `xml <file>` | Validate and pretty-print an XML file. |

## Files

| Command | Usage | Description |
|---|---|---|
| `append` | `append <file> <text>` | Append a line of text to a file. |
| `basename` | `basename <path>` | Print the final path component. |
| `cat` | `cat <file...>` | Read one or more text files. |
| `cd` | `cd [path\|-]` | Change the current directory. |
| `cp` | `cp [-r] <source> <destination>` | Copy files or folders. |
| `dirname` | `dirname <path>` | Print the parent directory. |
| `dirs` | `dirs` | Show the directory stack. |
| `drives` | `drives` | List available Windows drives. |
| `exists` | `exists <path>` | Check whether a path exists. |
| `ls` | `ls [-a] [-l] [path]` | List files and folders. |
| `mkdir` | `mkdir <folder...>` | Create one or more folders. |
| `mv` | `mv <source> <destination>` | Move a file or folder. |
| `popd` | `popd` | Return to the last pushed directory. |
| `pushd` | `pushd <path>` | Push the current path and change directory. |
| `pwd` | `pwd` | Show the current directory. |
| `rename` | `rename <path> <new-name>` | Rename a file or folder. |
| `rm` | `rm [-r] [-f] <target...>` | Delete files or folders with safeguards. |
| `rmdir` | `rmdir [-r] <folder...>` | Remove empty or recursive folders. |
| `stat` | `stat <path>` | Show file or folder details. |
| `touch` | `touch <file...>` | Create files or update timestamps. |
| `tree` | `tree [path] [-d depth]` | Draw a directory tree. |
| `write` | `write <file> <text>` | Replace a file with text. |

## Files+

| Command | Usage | Description |
|---|---|---|
| `checksum` | `checksum <file>` | Calculate SHA-256, SHA-1, and MD5 file checksums. |
| `compare` | `compare <file1> <file2>` | Compare two files by size and SHA-256. |
| `countfiles` | `countfiles [path]` | Count files and folders recursively. |
| `diskfree` | `diskfree [path]` | Show free and total space for a drive. |
| `du` | `du [path]` | Calculate recursive disk usage for a path. |
| `duplicates` | `duplicates [path]` | Find duplicate files by size and SHA-256 with safe limits. |
| `emptyfiles` | `emptyfiles [path]` | List empty files under a folder. |
| `extension` | `extension <path>` | Print a path's extension. |
| `filesize` | `filesize <file>` | Show the exact and formatted size of a file. |
| `filetype` | `filetype <file>` | Guess a file type using extension and magic bytes. |
| `hexdump` | `hexdump <file> [bytes]` | Display a bounded hexadecimal file preview. |
| `join` | `join <destination> <file...>` | Join multiple files into one binary file. |
| `mktemp` | `mktemp [file\|dir]` | Create a uniquely named temporary item in the current folder. |
| `newest` | `newest [path]` | Find the newest item under a folder. |
| `oldest` | `oldest [path]` | Find the oldest item under a folder. |
| `pathinfo` | `pathinfo <path>` | Show normalized path components and status. |
| `realpath` | `realpath <path>` | Print a normalized absolute path. |
| `splitfile` | `splitfile <file> <chunk-bytes>` | Split a file into numbered chunks. |
| `strings` | `strings <file> [min-length]` | Extract printable ASCII strings from a local file. |
| `temp` | `temp` | Show the Windows temporary directory. |

## Kuro

| Command | Usage | Description |
|---|---|---|
| `about` | `about` | Show project, version, and creator information. |
| `changelog` | `changelog` | Show the current release notes. |
| `command-count` | `command-count` | Show the number of built-in commands. |
| `commandpacks` | `commandpacks` | Show command packs and their sizes. |
| `copyright` | `copyright` | Show the Kuro copyright notice. |
| `credits` | `credits` | Show Kuro project credits. |
| `dashboard` | `dashboard` | Show a compact live Kuro dashboard. |
| `doctor` | `doctor` | Check Kuro configuration and environment health. |
| `features` | `features` | Show Kuro's main feature set. |
| `fortune` | `fortune` | Print a short Kuro quote. |
| `identity` | `identity` | Show the shell identity and local user. |
| `kuro` | `kuro` | Show the Kuro identity banner. |
| `license` | `license` | Show the included use notice. |
| `profile` | `profile` | Show your saved Kuro personalization profile. |
| `session` | `session` | Show details about the current Kuro session. |
| `shortcuts` | `shortcuts` | Show keyboard shortcuts. |
| `tips` | `tips [all]` | Show a random tip or every tip. |
| `welcome` | `welcome` | Print a compact welcome banner. |
| `who-built-this` | `who-built-this` | Show who created Kuro. |

## Network

| Command | Usage | Description |
|---|---|---|
| `cidr` | `cidr <ipv4/prefix>` | Calculate IPv4 CIDR network information. |
| `dns` | `dns <host>` | Resolve a host name to IP addresses. |
| `hostcheck` | `hostcheck <host>` | Resolve a host and summarize returned addresses. |
| `ipclass` | `ipclass <ip>` | Classify an IP as public, private, loopback, or multicast. |
| `netinfo` | `netinfo` | Show active network adapters and addresses. |
| `ping` | `ping <host> [count]` | Send up to ten ICMP echo requests. |
| `portcheck` | `portcheck <host> <port>` | Test one TCP port on an authorized host. |
| `portname` | `portname <port>` | Show a common service name for a port. |
| `ports` | `ports` | List active TCP listeners and connections. |
| `publicip` | `publicip` | Show your public IP using a simple external echo service. |

## OSINT

| Command | Usage | Description |
|---|---|---|
| `domainlookup` | `domainlookup <domain>` | Look up a domain through public RDAP data. |
| `domainparts` | `domainparts <domain\|url>` | Break a host name into basic labels. |
| `emailcheck` | `emailcheck <address>` | Validate email syntax and resolve its public domain. |
| `iplookup` | `iplookup <ip>` | Look up an IP allocation through public RDAP data. |
| `rdap` | `rdap <domain\|ip>` | Look up public registration data through RDAP. |
| `reverse-dns` | `reverse-dns <ip>` | Perform a reverse DNS lookup. |
| `robots` | `robots <domain\|url>` | Fetch a site's public robots.txt file. |
| `sitemap` | `sitemap <domain\|url>` | Fetch a site's public sitemap.xml preview. |

## Privacy

| Command | Usage | Description |
|---|---|---|
| `urlclean` | `urlclean <url>` | Remove common tracking parameters from a URL. |

## Search/Text

| Command | Usage | Description |
|---|---|---|
| `find` | `find <pattern> [path]` | Recursively find files and folders. |
| `grep` | `grep [-i] <text> <file>` | Search for text inside a file. |
| `head` | `head [-n count] <file>` | Show the first lines of a file. |
| `line` | `line <number> <file>` | Print one numbered line from a text file. |
| `replace` | `replace <file> <old> <new>` | Replace text inside a UTF-8 text file. |
| `sort` | `sort [-r] <file>` | Sort the lines of a file. |
| `tail` | `tail [-n count] <file>` | Show the last lines of a file. |
| `uniq` | `uniq <file>` | Remove consecutive duplicate lines. |
| `wc` | `wc <file>` | Count lines, words, characters, and bytes. |

## Security

| Command | Usage | Description |
|---|---|---|
| `ethics` | `ethics` | Show the authorization and safety rules for security tools. |
| `security` | `security` | Show Kuro's ethical-security command guide. |

## Shell

| Command | Usage | Description |
|---|---|---|
| `alias` | `alias [name=command]` | Create or list persistent command aliases. |
| `categories` | `categories` | List command categories and command counts. |
| `clear` | `clear` | Clear the terminal screen. |
| `commands` | `commands` | Print every built-in command name. |
| `env` | `env [--all]` | List Kuro variables or all environment variables. |
| `exit` | `exit` | Close Kuro. |
| `help` | `help [command]` | Show all commands or detailed help. |
| `history` | `history [count\|-c]` | Show or clear command history. |
| `set` | `set NAME=value` | Create a persistent Kuro variable. |
| `unalias` | `unalias <name>` | Remove a saved alias. |
| `unset` | `unset <NAME>` | Remove an Kuro variable. |
| `version` | `version` | Show the Kuro version. |
| `which` | `which <command>` | Locate a built-in, alias, or executable. |

## System

| Command | Usage | Description |
|---|---|---|
| `date` | `date` | Show the current date. |
| `edit` | `edit <file>` | Open a file in Notepad. |
| `exec` | `exec <program> [arguments]` | Run a console program and capture its output. |
| `explorer` | `explorer [path]` | Open a folder in File Explorer. |
| `hostname` | `hostname` | Show the computer name. |
| `kill` | `kill <pid>` | Stop a process by numeric ID. |
| `memory` | `memory` | Show Kuro process memory information. |
| `open` | `open [path\|url]` | Open a path or URL with Windows. |
| `os` | `os` | Show operating-system information. |
| `processes` | `processes [filter]` | List running processes. |
| `sysinfo` | `sysinfo` | Show detailed runtime and system information. |
| `time` | `time` | Show the current local time. |
| `uptime` | `uptime` | Show Windows uptime. |
| `whoami` | `whoami` | Show your Windows username. |

## System+

| Command | Usage | Description |
|---|---|---|
| `admin` | `admin` | Show whether Kuro is elevated. |
| `appdir` | `appdir` | Show the running Kuro application directory. |
| `beep` | `beep [frequency] [milliseconds]` | Play a short bounded system beep. |
| `calendar` | `calendar [month] [year]` | Draw a monthly calendar. |
| `clipboard` | `clipboard [get\|set <text>\|clear]` | Read or update the Windows clipboard. |
| `configdir` | `configdir` | Show Kuro's configuration directory. |
| `cpu` | `cpu` | Show processor architecture and core counts. |
| `culture` | `culture` | Show current language and formatting culture. |
| `desktop` | `desktop` | Change to the user's Desktop folder. |
| `documents` | `documents` | Change to the user's Documents folder. |
| `downloads` | `downloads` | Change to the user's Downloads folder. |
| `envsize` | `envsize` | Show counts for environment variables and PATH entries. |
| `gc` | `gc` | Request a .NET garbage collection and show memory change. |
| `homepath` | `homepath` | Show the current user's home directory. |
| `machine` | `machine` | Show machine and process architecture details. |
| `now` | `now` | Show local and UTC date/time together. |
| `openconfig` | `openconfig` | Open Kuro's configuration folder. |
| `pathlist` | `pathlist` | List every directory in the Windows PATH. |
| `pid` | `pid` | Show Kuro's process ID. |
| `screen` | `screen` | Show the primary desktop dimensions. |
| `shellpath` | `shellpath` | Show the running Kuro executable path. |
| `specialfolders` | `specialfolders` | List useful Windows special folders. |
| `timeit` | `timeit <program> [arguments]` | Run an external program and report elapsed time. |
| `timezone` | `timezone` | Show local time-zone information. |
| `wait` | `wait <milliseconds>` | Pause Kuro for up to five seconds. |
| `week` | `week` | Show the current ISO week number. |

## Utilities

| Command | Usage | Description |
|---|---|---|
| `base64` | `base64 <encode\|decode> <text>` | Encode or decode Base64 text. |
| `calc` | `calc <expression>` | Evaluate arithmetic with + - * / % ^ and parentheses. |
| `echo` | `echo <text>` | Print text after variable expansion. |
| `hash` | `hash <sha256\|sha1\|md5> <text\|--file path>` | Hash text or a file. |
| `hex` | `hex <text>` | Convert UTF-8 text to hexadecimal. |
| `json` | `json <file>` | Pretty-print and validate a JSON file. |
| `length` | `length <text>` | Count characters in text. |
| `lower` | `lower <text>` | Convert text to lowercase. |
| `random` | `random [min] [max]` | Generate a random integer. |
| `repeat` | `repeat <count> <text>` | Repeat text up to 1,000 times. |
| `reverse` | `reverse <text>` | Reverse text. |
| `unhex` | `unhex <hex>` | Convert hexadecimal to UTF-8 text. |
| `upper` | `upper <text>` | Convert text to uppercase. |
| `uuid` | `uuid` | Generate a new UUID. |

## Web Audit

| Command | Usage | Description |
|---|---|---|
| `cookiecheck` | `cookiecheck <url>` | Review public Set-Cookie flags for a page. |
| `headers` | `headers <url>` | Fetch public HTTP response headers. |
| `redirects` | `redirects <url>` | Show a bounded HTTP redirect chain. |
| `securityheaders` | `securityheaders <url>` | Review common defensive HTTP security headers. |
| `status` | `status <url>` | Check HTTP status and response latency. |
| `tls` | `tls <host> [port]` | Inspect a server TLS certificate without exploitation. |
| `useragent` | `useragent` | Show the HTTP user agent used by Kuro tools. |
| `webcheck` | `webcheck <url>` | Run a compact status and security-header review. |
