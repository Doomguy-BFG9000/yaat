# Names the client release tag deploy-to-droplet.ps1's publish check compares against.
# Dot-sourced by deploy-to-droplet.ps1; defines functions only, so it can be loaded on its
# own (e.g. `. .\deploy-release-tag.ps1; Resolve-ReleaseTag ...`) without deploying anything.

# A release tag name: v<major>.<minor>.<patch> with an optional pre-release suffix (v0.16.2-beta).
function Test-ReleaseTagName {
  param([string]$Name)
  return ($Name -cmatch '^v[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?\z')
}

# Returns @{ Tag; Source }: the release tag to check and where its name came from.
#   -ReleaseTag given             -> that tag ("-ReleaseTag")
#   -ClientRef is a release tag   -> that tag ("-ClientRef")
#   neither                       -> v<Version> from Directory.Build.props ("Directory.Build.props");
#                                    Tag is $null when the file has no <Version>.
# Throws when -ReleaseTag is not a release tag name, or when both name a tag and they differ.
function Resolve-ReleaseTag {
  param(
    [Parameter(Mandatory)][AllowEmptyString()][string]$ClientRef,
    [Parameter(Mandatory)][AllowEmptyString()][string]$ReleaseTag,
    [Parameter(Mandatory)][string]$PropsPath
  )

  if ($ReleaseTag -and -not (Test-ReleaseTagName $ReleaseTag)) {
    throw "-ReleaseTag '$ReleaseTag' is not a release tag name (expected v<major>.<minor>.<patch>[-suffix], e.g. v0.16.2-beta)."
  }

  $clientRefTag = if (Test-ReleaseTagName $ClientRef) { $ClientRef } else { $null }
  if ($ReleaseTag -and $clientRefTag -and ($ReleaseTag -cne $clientRefTag)) {
    throw "-ReleaseTag $ReleaseTag and -ClientRef $clientRefTag name different releases. Pass the same tag to both, or drop -ReleaseTag."
  }

  if ($ReleaseTag) {
    return [pscustomobject]@{ Tag = $ReleaseTag; Source = "-ReleaseTag" }
  }
  if ($clientRefTag) {
    return [pscustomobject]@{ Tag = $clientRefTag; Source = "-ClientRef" }
  }

  $props = Get-Content $PropsPath -Raw
  if ($props -notmatch "<Version>([^<]+)</Version>") {
    return [pscustomobject]@{ Tag = $null; Source = "Directory.Build.props" }
  }
  return [pscustomobject]@{ Tag = "v$($Matches[1])"; Source = "Directory.Build.props" }
}
