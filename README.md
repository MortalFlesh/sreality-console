Sreality Console
================

![Check](https://github.com/MortalFlesh/sreality-console/workflows/Check/badge.svg)

> Standalone console application for accessing sreality REST api

## Features
It is a standalone (self-contained) _one-file_ console application, which allows you to access a sreality REST api and
- save results
    - to file
    - to google sheets
- compare previous and current result, to determine what is new
- send you a notification, when anything new is presented

## Configuration
Configuration must be in the file `.sreality.json` in the same directory, where the console is executed.

### .sreality.json
```json
...todo...
```

---
### Development

First run:
```
paket install
./build.sh
```

or `./build.sh -t Watch`

List commands
```sh
bin/console list
```

Run tests locally
```sh
./build.sh -t Tests
```
