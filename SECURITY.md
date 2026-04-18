# Security

## Reporting a vulnerability

Please report security issues privately via GitHub's **Report a vulnerability**
button on the [Security tab](https://github.com/umage-ai/Umage.Optimizely.EditorPowertools/security/advisories),
or by email to the maintainers listed in the README. Do not open a public issue
for vulnerability reports.

## Known transitive vulnerabilities

EditorPowertools depends on `EPiServer.CMS` and `EPiServer.CMS.UI.Core`. Those
packages carry their own transitive dependencies, some of which currently have
known CVE advisories. We cannot fix these from within this add-on — a patched
version of the affected EPiServer package would need to be released upstream.

The list below reflects the state as of the latest release tag.

### CMS 13 (`net10.0`)

| Package | Version | Severity | Advisory |
|---------|---------|----------|----------|
| `MailKit` | 4.15.1 | Moderate | [GHSA-9j88-vvj5-vhgr](https://github.com/advisories/GHSA-9j88-vvj5-vhgr) |

### CMS 12 (`net8.0`)

| Package | Version | Severity | Advisory |
|---------|---------|----------|----------|
| `MailKit` | 3.0.0 | Moderate | [GHSA-9j88-vvj5-vhgr](https://github.com/advisories/GHSA-9j88-vvj5-vhgr) |
| `MimeKit` | 3.0.0 | High | [GHSA-gmc6-fwg3-75m5](https://github.com/advisories/GHSA-gmc6-fwg3-75m5) |
| `MimeKit` | 3.0.0 | Moderate | [GHSA-g7hc-96xr-gvvx](https://github.com/advisories/GHSA-g7hc-96xr-gvvx) |
| `SixLabors.ImageSharp` | 2.1.9 | High | [GHSA-2cmq-823j-5qj8](https://github.com/advisories/GHSA-2cmq-823j-5qj8) |
| `SixLabors.ImageSharp` | 2.1.9 | Moderate | [GHSA-rxmq-m78w-7wmc](https://github.com/advisories/GHSA-rxmq-m78w-7wmc) |

All entries above are **transitive** — they are not direct dependencies of
`UmageAI.Optimizely.EditorPowerTools`. The EditorPowertools code base does not
invoke the affected APIs directly.

### What consumers can do

If your deployment's threat model requires closing these advisories:

- Override the transitive version in your host project by adding a top-level
  `<PackageReference>` to the patched version. NuGet's **direct dependency wins**
  rule lets you pin newer versions than the one EPiServer transitively resolves:

  ```xml
  <ItemGroup>
    <PackageReference Include="MailKit" Version="4.17.0" />
    <PackageReference Include="SixLabors.ImageSharp" Version="3.1.11" />
  </ItemGroup>
  ```

  Only do this if your build still passes end-to-end — EPiServer may assume
  a specific transitive range and break on mismatched majors.

- Track upstream for patched releases of `EPiServer.CMS.UI.Core`.

## Changelog

### 0.6.0

- Removed dependency on `EPiServer` via `EPPlusFree` 4.5.3.8. Replaced with
  `ClosedXML` for Excel import/export. This eliminated a Critical advisory on
  `System.Drawing.Common` ([GHSA-rxg9-xrhp-64gj](https://github.com/advisories/GHSA-rxg9-xrhp-64gj))
  and a High advisory on `System.IO.Packaging` ([GHSA-f32c-w444-8ppv](https://github.com/advisories/GHSA-f32c-w444-8ppv))
  that were pulled transitively through the old Excel library.

### 0.5.1

- Relaxed `EPiServer.CMS` / `EPiServer.CMS.UI.Core` references from exact-pin
  to `12.*` / `13.*` to avoid unresolvable conflicts with sibling EPiServer
  packages in consumer projects.
