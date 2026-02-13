# MRP.Assistant - Epicor MRP Log Investigation Assistant

## Purpose
A .NET class library for parsing, analyzing, and comparing Epicor Kinetic MRP log files. 
Helps planners and IT teams understand what changed between MRP runs and why.

## Project Structure
- **/Core** - Domain models (MrpLogEntry, MrpLogDocument, etc.)
- **/Parsers** - Log parsing logic
- **/Analysis** - Comparison and explanation engines
- **/Reporting** - Report generation

## Usage
See MRP.Assistant.CLI for command-line interface.

## Dependencies
- .NET 9.0
- No external dependencies in core library

## Test Data
Sample MRP logs available in `/testdata/`
