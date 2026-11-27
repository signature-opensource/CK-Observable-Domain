# CK.Observable.PersistentDomainDriver

This package gives an observable domain driver that saves its state to disk and loads it again at the next start.
Each save is a new file with a checksum. The driver detects a damaged file before it loads the file.
This document is for operators and for developers who deploy a host with this driver.

## 1. What the driver stores and where

### Folder

If the option `StorePath` is not set, the folder is:

```
<StoreRootPath>/#<Environment>/<FullName>/-Local/ObservableDomains/<DomainName>/
```

- `<StoreRootPath>` is the AppIdentity store root. The default is `%LOCALAPPDATA%\CK-AppIdentity`.
- `<Environment>` is the environment name, for example `Production`.
- `<FullName>` is the full name of the party, for example `Acme/$Server`.
- `<DomainName>` is the name of the domain, for example `Inventory`.

If the option `Domains:<DomainName>:StorePath` is set, this folder replaces the default folder.
The path must be fully qualified. If the path is relative, the start fails with an error in the log.
Each domain has its own folder.

### Files

| File | Meaning |
|---|---|
| `<DomainName>.<Generation>.od` | A generation. This is one complete snapshot of the domain. |
| `<DomainName>.<Generation>.od.damaged` | A generation that failed the checksum or the length check. The driver keeps it for investigation. |
| `<DomainName>.<Generation>.od.unloadable` | A generation with a valid checksum that the current code cannot read, or a file of a newer format version (after a package downgrade). The cause is in the code, not on the disk. |
| `<DomainName>.<Generation>.od.superseded` | A valid generation that the driver did not try before it created a new domain. |
| `<DomainName>.<guid>.tmp` | A save in progress. The driver deletes an orphan `.tmp` file at the next start. |
| `<DomainName>.lock` | The process lock. Leave this file in place. |

`<Generation>` is a save counter with 19 digits. A file with a higher number is newer.
The counter is not a date and not a transaction number.

Rules for the files:

- The driver keeps the newest `GenerationCount` `.od` files and deletes the older ones.
- The retention counts the `.od` files. It does not load them to check them.
  If a domain saves at a high rate, all the kept files can be from the last few seconds.
  For such a domain, use a positive `SnapshotSaveDelay` or a larger `GenerationCount`.
- The driver deletes a `.damaged` or `.superseded` file after `DamagedFileKeepDuration`. The age starts at the rename.
- A `.superseded` file can hold a valid copy of the old state.
  To keep this state, use the procedure of section 4 before `DamagedFileKeepDuration` ends.
- The driver never deletes an `.unloadable` file. Only an operator can delete it.
- If the driver cannot rename a file to its mark (for example, an antivirus holds the file), the file keeps its name.
  The driver does not delete this file, and it tries the rename again after each save.
  This protection stops with the process: after a restart, the file is an old generation, and the driver deletes it as usual.
- One process at a time owns the folder. If a second process starts on the same folder, its start fails.
- If the first process dies, the operating system releases the lock. The `.lock` file stays on disk, and this is normal.

The driver reports what it did at each start. The kinds are:

- `Created`: the folder had no `.od` file. The driver created a new domain.
- `Loaded`: the newest file loaded.
- `LoadedFromFallback`: the newest file failed and an older generation loaded.
- `CreatedAfterLoadFailure`: no allowed file loaded, and `CreateOnLoadError` is true. The driver created a new domain.
- `Failed`: the start failed. The driver changed no file.

The driver renames the files that it did not load (`.damaged`, `.unloadable` and `.superseded`) during the first save of the start.
It renames them after the flush of the new file and before the rename of the new file. So the new generation never exists before the driver tries the marks.

The driver writes the first save of a start only after the initial transaction succeeds.
If the start fails before its first save, the driver changes no `.od` file and no mark, and a second start gives the same result.
Before the first save, the start only creates the `.lock` file and deletes the orphan `.tmp` files.
This is true for a `Failed` outcome and for a load handler that returns false.
It is also true for an error in `PreConfigure`, `InitializeNewDomain`, a configurator or `PostConfigure`.
It is also true for a timed event that fails during the start.
The start does not wait for the domain post actions of the initial transaction. An error in one of them does not stop the start.

In two cases, a start that fails changes files:

- An error in the rename of the new file, or a power cut after the marks and before this rename.
  The marks stay, but there is no new generation.
  The next start reports `Created` instead of `CreatedAfterLoadFailure`, or `Loaded` instead of `LoadedFromFallback`.
  The marked files stay in the folder.
- A commit of the timer runs just after the driver attaches its saver. Its save writes the first file of the start, with the marks.
  Then the first save of the driver fails. The failed start leaves this new generation and the marks.
  The next start loads the new generation and reports `Loaded`.

The log of the failed start tells what happened.

### The timer of the domain at start

The load always stops the timer of the domain, whatever the timer state in the file. So no timed event runs before `PreConfigure`.
If `AutoStartTimer` is true, the last step of the initial transaction starts the timer. The timed events that are due then run at the end of this transaction.
If `AutoStartTimer` is false, the timer stays stopped after the start. The driver does not restore the timer state of the file.
Application code can start the timer in a later transaction.

Operators need only the file meanings and the procedures in this document.
The internal order of the start and stop steps is not part of the contract.

## 2. Deployment

- The domain keeps the root service provider of the host for its life. A rollback instantiates the sidekicks again with it.
  So a service that a sidekick gets in its constructor must be a singleton. A scoped service resolved from the root provider
  is never disposed, and the container can refuse it.

- A service that runs as LocalSystem must set the AppIdentity option `StoreRootPath`.
  Without it, the files can go under the `systemprofile` folder.
- Three changes point the driver at a new empty folder: the environment name, the party `FullName` and `StoreRootPath`.
  The start then reports `Created` and no error.
  The driver does not detect this case.
- If a start reports `Created` and the host already has state, check these three values first.
  Then move the files to the new folder by hand. Stop the service before you move the files.
- The driver ignores the old global key `CK-ObservableDomain:StorePath` and logs no error.
  A host that still sets this key uses the AppIdentity folder, and its first start reports `Created`.
  Remove the key. To keep a specific folder, set `CK-ObservableDomain:Domains:<DomainName>:StorePath`.
  The driver does not read the old `<DomainName>.od` file. Delete it after the first start.

## 3. Hardware

The driver flushes each file to the disk before it renames the file. This is always on.
The flush protects the files against a power cut.

Two known causes remove this protection:

- The Windows option "Turn off Windows write-cache buffer flushing" in Device Manager.
- Storage that reports a flush before the flush is complete.

With these causes, a power cut can leave a file of the right size with wrong content.
The checksum then detects the damage, and the recovery options decide what happens.
Do not use this option on a machine that stores a domain with this driver.

On Linux, the driver flushes the file but not the folder.
A power cut can lose the last rename and so one save. It cannot give a damaged file.

## 4. Manual recovery

Stop the service before you change files in the folder.

To go back to an older state:

1. Move the newest `.od` files out of the folder.
2. Start the service. The driver loads the newest `.od` file in the folder.

To use a marked file again, remove its suffix (`.damaged`, `.unloadable` or `.superseded`).
For example, rename `X.0000000000000000042.od.unloadable` to `X.0000000000000000042.od`.

Do not delete an `.unloadable` file before you know the cause.
The checksum of an `.unloadable` file is correct, so its bytes are not damaged.
Usually, the cause is a change in the code: the new code cannot read the old snapshot.
After a rollback of the code, remove the suffix. The driver can then load the file.

### Get the old state back after the driver created a new domain

When `CreateOnLoadError` is true, the driver can create a new domain while valid files of the old state are in the folder.
This occurs in two cases:

- The newest file is `.unloadable`.
- The newest file and the `FallbackGenerationCount` older files that the driver tried are damaged.
  Older files that the driver did not try can be valid.

In these cases, the driver adds the `.superseded` suffix to the files that it did not try.
These files can hold valid copies of the old state.
At the end of `DamagedFileKeepDuration`, the driver deletes them. Do this procedure before that time:

1. Stop the service.
2. Move the `.od` files of the new domain out of the folder. These files have the highest generation numbers.
3. Remove the `.superseded` suffix from the files of the old state.
4. If a file of the old state has the `.unloadable` suffix, roll back the code.
5. After the rollback of the code, remove the `.unloadable` suffix from this file.
6. Start the service.

## 5. Options

The configuration section is `CK-ObservableDomain`. Each option is per domain, under `Domains:<DomainName>:`.
There is no global option. The driver ignores the old global key `StorePath` (see section 2).
To move all domains at once, set the AppIdentity `StoreRootPath`.

| Option | Default | Meaning |
|---|---|---|
| `StorePath` | null (AppIdentity local store) | The folder of the domain. It must be a fully qualified path. An empty value is the same as null, because a JSON `null` gives an empty value. |
| `SnapshotSaveDelay` | zero | Zero saves after each commit. A positive value (1 day at most) saves at most once per delay, and a trailing save follows the last commit. A negative value turns off automatic saves and the retries. Saves then happen only in a transaction that calls `SendSnapshotCommand()`, and at graceful stop. See section 6. |
| `GenerationCount` | 5 | The number of `.od` files that the driver keeps. It must be 1 or more. With 1, the folder has a single copy of the state, and a damaged file has no older file to fall back to. |
| `FallbackGenerationCount` | 0 | The number of older generations that the driver tries after the newest file is damaged. It must be lower than `GenerationCount`. An unloadable file stops the fallback. |
| `CreateOnLoadError` | false | If true, the driver creates a new domain after all allowed files fail. If false, the start fails. |
| `DamagedFileKeepDuration` | 7 days | The time that the driver keeps a `.damaged` or `.superseded` file. It must be between 0 and 3650 days. |
| `CompressionKind` | None | The compression of the snapshot. `GZiped` is the other value. A number other than 0 or 1 fails the start. |
| `SkipTransactionCount` | 0 | Full transacted mode. See `MemoryTransactionProviderClient.SkipTransactionCount`. It must be -1 or more. |
| `ExportedEventKeepDuration` | 5 minutes | The time that exported transaction events stay available to watchers. It must not be negative and not longer than 1 day. |
| `ExportedEventKeepLimit` | 10 | The minimum number of exported transaction events kept. It must be 1 or more. |
| `DebugMode` | false | Sentinel bytes: the driver writes them at each save and checks them at each load. |

Examples:

- Strict (the default): `FallbackGenerationCount` 0 and `CreateOnLoadError` false. A damaged newest file stops the start, and an operator repairs the folder.
- Resume with a warning: `FallbackGenerationCount` 1 and `CreateOnLoadError` false, plus a load handler that sends a mail.
- A device that the server can rebuild: `FallbackGenerationCount` 4 and `CreateOnLoadError` true.

A load handler is a class that implements `IPersistentDomainLoadHandler<T>`.
The driver calls each handler after the load with a `PersistentDomainLoadOutcome`.
If the start fails, the driver also calls the handlers with the `Failed` outcome, so that a handler can send an alert.

## 6. How the driver saves

### Who pays for a save

The driver has no background writer. A commit that is due for a save writes the file in a post action of its transaction.
So the code that runs this transaction waits for the write, the flush to disk, the rename and the retention.
On an SSD this takes some milliseconds. On eMMC or on a hard disk it takes more.
With the default `SnapshotSaveDelay` (zero), each commit pays this cost.
A positive `SnapshotSaveDelay` limits the cost to one save per delay.

### Saves that no commit carries

The driver runs no transaction of its own to save. In these cases, it copies the last committed state under the read lock of the domain,
then writes the copy outside the lock:

- The trailing save at the end of a delay window.
- A retry after a lost save.
- The final save at graceful stop.
- The first save of a start.

If a save fails, or if the domain skips the save of a commit, the driver tries again.
After a failed write, the first retry comes after 1 second. After each new failed write, the wait doubles, up to 60 seconds.
After a skipped save, the retry waits for `SnapshotSaveDelay`, and at least 1 second.
With a negative `SnapshotSaveDelay`, there is no retry.

### Stop and restart

If the last committed state is not saved yet at graceful stop, the driver writes a final save. Then it applies the retention,
releases the lock and disposes the domain.
A restart with no change writes one generation: the first save of the start. A stop with no change writes no generation.
So a restart with no change pushes one older generation out of the retention.

### Watchers

Because the driver saves with no transaction of its own, a save adds no transaction and sends no event to the watchers.
A transaction of the application that changes nothing sends an event with no change.
The `JsonEventCollector` sends an event for each transaction, so the transaction numbers that a client gets have no gap.

## 7. When the persistence saves nothing

The driver keeps a copy of the last committed state in memory. A failed transaction restores this copy.
In two cases, the state in memory cannot be trusted:

- The restore after a failed transaction throws. The domain is then half-loaded.
- The creation of the copy throws during a commit. The copy is then half-written, and the next restore reads it.

Then the driver stops all saves until the next start, so that no wrong state replaces the good generations:

- It logs a fatal error once.
- It writes no file and applies no retention. There is no retry, and the stop writes no final save.
- The property `IsPersistenceHealthy` of the driver is false.

The domain continues to run in memory. The next start loads the last good generation.
The commits after the failure are lost. If `IsPersistenceHealthy` is false, restart the service. Then look for the cause in the log.
Usually the cause is a type whose serialization or deserialization throws.
