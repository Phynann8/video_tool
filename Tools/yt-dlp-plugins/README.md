# DramaBox yt-dlp plugin

This directory contains the custom yt-dlp extractor for `dramaboxdb.com`.

## Install

Copy the `yt_dlp_plugins` directory into one of these locations:

```text
<directory-containing-yt-dlp.exe>/yt-dlp-plugins/
%APPDATA%/yt-dlp/plugins/
```

The resulting layout must include:

```text
yt-dlp-plugins/
  dramabox/
    yt_dlp_plugins/
      extractor/
        dramabox.py
```

For the published Universal Media Downloader build, place that layout beside
`out_publish/yt-dlp.exe`:

```text
out_publish/
  yt-dlp.exe
  yt-dlp-plugins/
    dramabox/
      yt_dlp_plugins/
        extractor/
          dramabox.py
```

## Usage

The extractor recognizes DramaBox URLs containing a numeric book id, for
example:

```text
https://www.dramaboxdb.com/drama/41000122558/the-ceo
```

Use the normal yt-dlp command line. A DramaBox URL produces a playlist with
one entry per episode exposed by the API or page metadata.

## Authentication and troubleshooting

The extractor first requests a guest token from the DramaBox mobile API. To
reuse a token, set `DRAMABOX_TOKEN` before starting yt-dlp. The value may be a
bare token or a `Bearer ...` value.

Set `DRAMABOX_DISABLE_API=1` to skip the mobile API and use the page's
`__NEXT_DATA__` metadata only.

The API can reject requests with HTTP 403 or reject expired tokens. Those
responses are controlled by the upstream service and cannot be resolved by a
local retry alone.

## Dependencies

`dramabox.py` uses only yt-dlp and Python's standard library. RSA-SHA256
signing is implemented in pure Python, so the plugin does not require
`cryptography`, `Crypto`, or `Cryptodome` in a frozen yt-dlp executable.
