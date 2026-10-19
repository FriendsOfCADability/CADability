# CADability — Project Instructions

## Releasing a new version to NuGet

The package on nuget.org is published by `.github/workflows/nuget.yml`. The
workflow packs `CADability/CADability.csproj` **as it exists on the ref the
workflow runs from** — the tag name is never used as the version number, so the
csproj is the single source of truth for what gets published.

1. **Bump the version.** Edit `<Version>` in `CADability/CADability.csproj`
   (near the top of the first `<PropertyGroup>`). Follow SemVer, and read it
   strictly: a minor bump means public API was **added**, a patch bump means
   existing behaviour was **fixed**. Do not reach for a minor bump merely
   because a fix changes results that callers can notice — every fix worth
   making does that, so by that reading a patch release could never exist.
   Say what moved in the release notes instead. Check whether the release
   really adds public surface before deciding: an `internal` type is not
   public API, whatever its members are declared as. Versions on nuget.org are
   immutable — a broken release cannot be replaced, only superseded by a
   higher version.
2. **Get that commit onto `master`.** The release is always cut from `master`,
   so merge the feature branch (or its pull request) first.
3. **Start the workflow.** Either trigger works and both do exactly the same
   thing:
   - push a tag: `git tag -a vX.Y.Z <commit> -m "Version X.Y.Z"` followed by
     `git push origin vX.Y.Z` (the `push: tags: ['v*']` trigger), or
   - start it by hand: Actions → *Publish NuGet Package* → *Run workflow* →
     branch `master` (the `workflow_dispatch` trigger).

   Use the manual trigger whenever the tag cannot be pushed (see below). It
   publishes the identical package, because the version comes from the csproj
   and not from the tag.
4. **Verify.** The *Push NuGet package* step must log
   `Your package was pushed.` — do not infer success from a green run alone,
   because the push uses `--skip-duplicate` and therefore also succeeds
   quietly when the version is already on nuget.org. After that it takes a few
   minutes for nuget.org to validate and index the package, so
   `https://api.nuget.org/v3-flatcontainer/cadability/index.json` still returns
   404 for the fresh version for a while. That is expected and not a failure.

   The build also produces a symbol package (`CADability.<version>.snupkg`)
   next to the `.nupkg`. `dotnet nuget push` uploads it along with the package
   without the workflow naming it, so the log shows a second upload. Symbols
   let consumers step into CADability sources from their own debugger, so if
   that upload ever stops happening, the release is incomplete even though the
   package itself is fine.
5. **Make sure the tag exists.** Every `1.1.x` release is tagged. If the
   release was started with `workflow_dispatch`, create and push the matching
   `vX.Y.Z` tag afterwards from a normal clone so the history keeps marking the
   released commits.
6. **Publish release notes. Every release gets them, without exception.**
   Create a GitHub release for the tag (Releases → *Draft a new release* →
   choose `vX.Y.Z`, title `CADability X.Y.Z`) whose text says what is new,
   which issues are fixed, what changed in behaviour, and whether consumers
   have to recompile (a public field turned into a property, for example, is
   a binary break that the notes must state prominently). The merged pull
   requests' descriptions and `TODO.md` are the sources. Write the notes
   while the changes are fresh, at the latest when the tag is pushed; a
   release without notes is not finished. Claude Code cloud sessions cannot
   create the GitHub release themselves, so they prepare the text as a
   Markdown file for the person doing the release.

## Known limitation: tags cannot be pushed and branches cannot be deleted from Claude Code cloud sessions

Pushing to `refs/tags/*` from a Claude Code cloud session fails, while pushing
branches to the same repository with the same credentials works. Both annotated
and lightweight tags are affected. Deleting a remote branch
(`git push origin --delete <branch>`) fails the same way, while creating and
updating branches, including `git push --force-with-lease` on a branch of the
session, works. The symptom is:

```
error: RPC failed; HTTP 403 curl 22 The requested URL returned error: 403
send-pack: unexpected disconnect while reading sideband packet
fatal: the remote end hung up unexpectedly
Everything up-to-date
```

Two things make this easy to misread:

- git prints `Everything up-to-date` **after** the error. For a tag it **exits
  with status 0**, so a script or agent that only checks the exit code
  concludes the tag was pushed. Always verify with `git ls-remote --tags origin`.
  A branch deletion exited with status 1, but the output reads like success
  all the same; verify with `git ls-remote --heads origin <branch>`.
- The `unexpected disconnect` line looks like a transient network problem and
  invites retrying. It is not transient — the `HTTP 403` on the line above is
  the real cause, and retrying with backoff never succeeds.

This is not the outbound HTTPS proxy: its status endpoint records no denial for
`github.com` when the push fails. It is the git credentials the session is
given, which cover creating and updating branch refs, but not tag refs and not
deleting refs. The GitHub tools of the session have no way to delete a branch
either.

**Workaround:** release via the `workflow_dispatch` trigger described above,
and push the tag later from a normal clone. Merged branches are deleted by a
person, with *Delete branch* on the page of the merged pull request or on the
*Branches* page of the repository.

Note that `git ls-remote --tags origin | tail -n 5` is misleading here, because
the output is sorted lexically rather than by version: pipe it through
`sort -V` before concluding which versions are tagged.

## Language

**Everything posted to GitHub is written in English.** That covers commit
messages, pull request titles and descriptions, issue text, review comments and
replies to them, and the files in this repository, this one included. Source
code comments are part of it too: inline comments, XML doc comments and
TODO/FIXME notes.

The language a task was discussed in makes no difference. A conversation held
in German still produces an English commit message.

Older German commit messages stay as they are — history is not rewritten for
this rule, and the rule is about what is written from now on.
