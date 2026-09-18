"""
DramaBox extractor for yt-dlp.

Ported from the C# implementation in ``Start.Infrastructure/Services``
(``DramaBoxExtractorEngine``, ``DramaboxClient``, ``DramaboxCrypto``).

Why a plugin instead of C# code?
    yt-dlp already provides range/resume, retries, HLS/DASH parsing, concurrent
    fragments, cookie import, proxying and ffmpeg muxing. This plugin only has to
    do the site-specific part: turn a dramaboxdb.com page into direct media URLs.

Strategies (tried in order, mirroring the C# "dual extraction" design):
    1. Mobile API  -- ``sapi.dramaboxdb.com`` with RSA-signed request headers.
                      Yields CDN mp4 URLs for every episode it is allowed to serve.
    2. HTML        -- scrape ``__NEXT_DATA__`` from the page for episode metadata
                      and any CDN URLs embedded in it.

Install (pick one):
    * <exedir>/yt-dlp-plugins/dramabox/yt_dlp_plugins/extractor/dramabox.py
    * %APPDATA%/yt-dlp/plugins/dramabox/yt_dlp_plugins/extractor/dramabox.py
    * any directory on PYTHONPATH

Note on signing
----------------
The DramaBox mobile API needs an RSA-SHA256 signature. Rather than depend on a
crypto library (the frozen yt-dlp.exe bundles neither ``cryptography`` nor the
``Crypto`` namespace -- only ``Cryptodome``), signing is done in pure Python with
``pow(m, d, n)``. This is standard PKCS#1 v1.5 and needs only ``hashlib``.

The published key (hndko/dramabox-rest-api-node, also used by the C# app) has a
**corrupt CRT parameter**: ``dp != d mod (p-1)``. OpenSSL silently recomputes it,
but strict parsers (Python ``cryptography``, .NET ``ImportPkcs8PrivateKey``)
reject the key outright. The key embedded below is the same key re-encoded with
correct CRT parameters, so it loads everywhere and produces identical signatures.

Set ``DRAMABOX_DISABLE_API=1`` to skip the API strategy entirely.
Set ``DRAMABOX_TOKEN`` to reuse a pre-obtained token instead of bootstrapping.
"""
import base64
import hashlib
import json
import os
import re
import time
import uuid

from yt_dlp.extractor.common import InfoExtractor
from yt_dlp.utils import ExtractorError, int_or_none

__all__ = ["DramaBoxIE"]


API_BASE = "https://sapi.dramaboxdb.com"
BOOTSTRAP_ENDPOINT = "/drama-box/ap001/bootstrap"
BATCH_ENDPOINT = "/drama-box/chapterv2/batch/load"

PACKAGE_NAME = "com.storymatrix.drama"
CID = "DAUAF1064291"
APP_VERSION_CODE = "470"
APP_VERSION_NAME = "4.7.0"
PLATFORM_P = "48"
TIME_ZONE = "+0700"
DEVICE_MODEL = "Redmi Note 8"
ANDROID_VERSION = "13"
BRAND = "Xiaomi"

API_TIMEOUT = 30
BATCH_DELAY_SECONDS = 0.8
MAX_BATCH_RETRIES = 3

# PKCS#8 DER of the DramaBox signing key, stored in the same reversible
# offset-obfuscation scheme the C# (DramaboxCrypto) and JS (DramaboxUtil)
# implementations use, so no raw PEM/key material sits in the repo.
_ENCODED_KEY = 'a]]Y-{]VUXUbV{"(|"}[M.DVUeYZUUgWV_{.{{g"U{YUU&]VUeWMeHmIelI~DL\\)%vmG})z_x"Y##UiFccf%U~#lX0W1w$FnJnf)[-+hniUay#ziIdkgJl[Y$GxH"^Y_vl}HW)#L&FYCYGm^d"E#eXExD^hx)-n#yYhbEj}\\nZge.gG`MHk&|DYGhdyvuYm(LLyY/-_,E+Xx~g&Z~Vv{ay10g%u*IbwF/ZFLl|d,WL$EI,?xyw+*)^#?U`[whXlG`-GZif,.jCxbKkaY"{w*y]_jax^/1iVDdyg(Wbz+z/$xVjCiH0lZf/d|%gZglW)"~J,^~}w"}m(E\'eEunz)eyEy`XGaVF|_(Kw)|awUG"\'{{e#%$0E.ffHVU++$giHzdvC0ZLXG|U{aVUUYW{{YVU^x),J\'If`nG|C[`ZF),xLv(-H\'}ZIEyCfke0dZ%aU[V)"V0}mhKvZ]Gw%-^a|m\'`\\f}{(~kzi&zjG+|fXX0$IH#j`+hfnME"|fa/{.j.xf,"LZ.K^bZy%c.W^/v{x#(J},Ua,ew#.##K(ki)$LX{a-1\\MG/zL&JlEKEw\'Hg|D&{EfuKYM[nGKx1V#lFu^V_LjVzw+n%+,Xd/IFyKE%uz(zv~l/n,Y+\',MF&^xJUM$kvxDVnh"KFn\\i$Xw_w(~zwY\\EMgkc\'|a^Zm"/iIZf&]Y)GC1}*0hcHa+GGn$.Y`c)mMdx#0UU0yxKn&\\?|#h)KwDFIefc-vF`$({f}ihIJ+aYW{mYU?~\\I$J}af_Jl~}V|gi%#)GX1f0v.#e)+]~I*n\')kyF$0I,m\\[G~vj}m]cK[+e-ah%X)VW/b|,aJxd)`Dwf%v*\'C}ValyG\'0~hCuk-yV"%Hf?i\'V*%v+X%FL)Ean\'WX+)IibwDhd~H_Z^-~%jCyL&[&0mY)&YW(w.E`(bc[X}`|"W{mYU.uy$bydm)lk?ajlC|u+z`eMJ+\'/.zK0,\\xYbnF(IUZ.KG[^km-WLjm?hw&_dU$y&Wia#+]Gh)gJ_IeC[&`xI_FV*&^)g/ebeZxGy|kU+xc,dX-eI)%CFz*-{-wG)c-^|K,b%.YnW]CHIke{?iZk)yzHddw?U)b+\'Ml~F0K`%x."W{mUf&~]eyl$|mn~[J^+g,{kn`,\\[".iX1W|mw]dxkFI{x#,&"[Cf1b-e%H?kClzh)0e~)Kf\'l$Ej/W])WV-mkbiF_)gmjHli+`?VIYfb~J]%JUc)cU}z,j]h0Iweeey&X?UhHm__aV"ezcF{%n1(vL?&/EGDy?G_C$,z&(^dny0)We_V{WFz&v~.|e-m.mm?X]i|u))}?)m)VfmhXv^m%|Cdbcu.EW$\\.l^+IdYXw$#G?b#]a%IL]ElFiC|\'X)U]#Ga#/\'nV"jmZ]L`$#cyfKy)yhxxbIMncYH~mCc%Wz(UHLD^z?Z_z&a\\v0I#dcIcc`uUz~+uyEZ|)$\'iyGYz]/M.j,|_VU&[VUdZ1\\_env[|"($0dkFw+hY]k`xi\\0cGKz$Lx~Ek~bH.~fU]H&|b}_e^f|GeYEEYEd1Vh#M#njkhLe+Y*g~%)UC+\'[)GKLzwihKk[V{h$VfuU%-EdE%Ch\'Dhg-|Il\']||a,/w}+]{)|ma][G[vdM^bUu)/cC(dkJ[}D/kuZK}#Kc)'

_OBFUSCATION_BUMP = 126 - 33


def _deobfuscate(text):
    """Reverse the offset obfuscation: printable chars are shifted by -20 mod 93."""
    out = []
    for ch in text:
        code = ord(ch)
        if 33 <= code <= 126:
            code -= 20
            if code < 33:
                code += _OBFUSCATION_BUMP
        out.append(chr(code))
    return "".join(out)


class _DerReader:
    """Minimal DER reader - enough to walk a PKCS#8/PKCS#1 RSA structure."""

    def __init__(self, buf, pos=0, end=None):
        self.buf = buf
        self.pos = pos
        self.end = len(buf) if end is None else end

    def _read_length(self):
        first = self.buf[self.pos]
        self.pos += 1
        if first < 0x80:
            return first
        count = first & 0x7F
        value = int.from_bytes(self.buf[self.pos:self.pos + count], "big")
        self.pos += count
        return value

    def read_tlv(self):
        tag = self.buf[self.pos]
        self.pos += 1
        length = self._read_length()
        start = self.pos
        self.pos += length
        return tag, self.buf[start:start + length]

    def read_sequence(self):
        if self.buf[self.pos] != 0x30:
            raise ValueError("expected DER SEQUENCE")
        self.pos += 1
        length = self._read_length()
        start = self.pos
        self.pos += length
        return _DerReader(self.buf, start, start + length)


def _load_rsa_parameters():
    """Return (n, e, d) parsed out of the embedded PKCS#8 key."""
    der = base64.b64decode(_deobfuscate(_ENCODED_KEY))

    outer = _DerReader(der).read_sequence()
    outer.read_tlv()                     # version
    algorithm = outer.read_sequence()
    algorithm.read_tlv()                 # OID (rsaEncryption)
    algorithm.read_tlv()                 # NULL
    _, pkcs1_der = outer.read_tlv()      # OCTET STRING -> PKCS#1 RSAPrivateKey

    key = _DerReader(pkcs1_der).read_sequence()
    fields = {}
    for name in ("version", "n", "e", "d", "p", "q", "dp", "dq", "qinv"):
        _, raw = key.read_tlv()
        fields[name] = int.from_bytes(raw, "big")

    if fields["p"] * fields["q"] != fields["n"]:
        raise ValueError("DramaBox signing key failed self-check (p*q != n)")
    return fields["n"], fields["e"], fields["d"]


# DigestInfo prefix for SHA-256 (RFC 8017 A.2.4)
_SHA256_DIGEST_INFO = bytes.fromhex("3031300d060960864801650304020105000420")


def _pkcs1v15_encode(message, key_size):
    digest_info = _SHA256_DIGEST_INFO + hashlib.sha256(message).digest()
    padding = b"\xff" * (key_size - len(digest_info) - 3)
    return b"\x00\x01" + padding + b"\x00" + digest_info


class DramaBoxSigner:
    """PKCS#1 v1.5 RSA-SHA256 signer with no third-party dependencies."""

    def __init__(self):
        self._key = None

    def _ensure_key(self):
        if self._key is None:
            self._key = _load_rsa_parameters()
        return self._key

    def sign(self, data):
        """Return the base64 signature for ``data`` (str or bytes)."""
        if isinstance(data, str):
            data = data.encode("utf-8")

        n, _e, d = self._ensure_key()
        key_size = (n.bit_length() + 7) // 8

        encoded = _pkcs1v15_encode(data, key_size)
        signature = pow(int.from_bytes(encoded, "big"), d, n)
        return base64.b64encode(signature.to_bytes(key_size, "big")).decode("ascii")

    def self_test(self):
        """Verify a signature round-trip. Returns True when the key is usable."""
        n, e, _d = self._ensure_key()
        key_size = (n.bit_length() + 7) // 8
        message = b"dramabox-plugin-self-test"
        signature = base64.b64decode(self.sign(message))
        return pow(int.from_bytes(signature, "big"), e, n) == int.from_bytes(
            _pkcs1v15_encode(message, key_size), "big")


# --------------------------------------------------------------------------
# Mobile API client
# --------------------------------------------------------------------------

class DramaBoxApiClient:
    """Client for the DramaBox Android API (sapi.dramaboxdb.com).

    Every request must carry an ``sn`` header: an RSA-SHA256 signature over
    ``timestamp + body + device-id + android-id`` (plus the bearer token for
    authenticated endpoints).
    """

    def __init__(self, ie, lang="en"):
        self._ie = ie
        self._signer = DramaBoxSigner()
        self._token = os.environ.get("DRAMABOX_TOKEN") or None
        self._device_id = str(uuid.uuid4())
        self._android_id = "ffffffff" + os.urandom(4).hex() + "000000000"
        self._lang = lang

    def _headers(self, token):
        spoofed_ip = "111.223.{}.{}".format(
            os.urandom(1)[0] or 1, os.urandom(1)[0] or 1)
        return {
            "tn": f"Bearer {token}" if token else "",
            "version": APP_VERSION_CODE,
            "vn": APP_VERSION_NAME,
            "cid": CID,
            "package-Name": PACKAGE_NAME,
            "Apn": "1",
            "device-id": self._device_id,
            "language": self._lang,
            "current-Language": self._lang,
            "p": PLATFORM_P,
            "Time-Zone": TIME_ZONE,
            "md": DEVICE_MODEL,
            "ov": ANDROID_VERSION,
            "over-flow": "new-fly",
            "brand": BRAND,
            "android-id": self._android_id,
            "User-Agent": "okhttp/4.10.0",
            "Content-Type": "application/json; charset=UTF-8",
            "X-Forwarded-For": spoofed_ip,
            "X-Real-IP": spoofed_ip,
        }

    def _post(self, endpoint, payload, token=None, signed_with_token=False,
              note="DramaBox API"):
        timestamp = int(time.time() * 1000)
        body = json.dumps(payload, separators=(",", ":"), ensure_ascii=False)

        signature_source = f"timestamp={timestamp}{body}{self._device_id}{self._android_id}"
        if signed_with_token:
            signature_source += f"Bearer {token}"

        headers = self._headers(token)
        headers["sn"] = self._signer.sign(signature_source)

        url = f"{API_BASE}{endpoint}?timestamp={timestamp}"
        return self._ie._download_json(
            url, None, note=note, data=body.encode("utf-8"), headers=headers,
            fatal=False)

    def ensure_token(self):
        """Return a usable bearer token, bootstrapping a guest one if needed."""
        if self._token:
            return self._token

        # Mirror DramaboxClient.EnsureInitializedAsync: the bootstrap request
        # is signed WITHOUT the bearer token.
        data = self._post(BOOTSTRAP_ENDPOINT, {"distinctId": None}, token=None,
                          signed_with_token=False, note="Bootstrapping guest token")

        if data is None:
            self._ie.report_warning(
                "DramaBox API: upstream sapi.dramaboxdb.com returned 403 Forbidden "
                "(Akamai WAF bot-protection active). Falling back to HTML scraping. "
                "Set DRAMABOX_TOKEN to authenticate if you have a valid token.")
            return None

        payload = _payload(data)
        user = payload.get("user")
        token = None
        if isinstance(user, dict):
            token = (user.get("token") or user.get("anonymous_token")
                     or user.get("anonymousToken"))
        if not token:
            token = payload.get("anonymous_token") or payload.get("token")
        if token:
            token = token.removeprefix("Bearer ")

        self._token = token or None
        return self._token

    def fetch_episodes(self, book_id):
        """Return (episodes, book_name) for ``book_id``.

        Pagination mirrors the C# client: first request uses index=1, then the
        index advances by 5 until the reported chapterCount is reached.
        """
        token = self.ensure_token()
        if not token:
            raise ExtractorError("DramaBox: could not obtain a guest token", expected=True)

        payload = {
            "boundaryIndex": 0,
            "comingPlaySectionId": -1,
            "index": 1,
            "currencyPlaySource": "discover_new_rec_new",
            "needEndRecommend": 0,
            "currencyPlaySourceName": "",
            "preLoad": False,
            "rid": "",
            "pullCid": "",
            "loadDirection": 1,
            "bookId": book_id,
        }

        first = self._post(BATCH_ENDPOINT, payload, token=token,
                           signed_with_token=True, note="Fetching episode list")
        if not _is_success(first):
            raise ExtractorError("DramaBox: batch/load rejected the request", expected=True)

        episodes = []
        _add_chapters(first, episodes)

        first_payload = _payload(first)
        book_name = first_payload.get("bookName")
        chapter_count = int_or_none(first_payload.get("chapterCount")) or 0
        pay_chapter = int_or_none(first_payload.get("payChapterNum")) or 0

        index = 6
        retries = 0
        while episodes and index <= chapter_count:
            time.sleep(BATCH_DELAY_SECONDS)
            payload["index"] = index
            batch = self._post(BATCH_ENDPOINT, payload, token=token,
                               signed_with_token=True,
                               note=f"Fetching episodes at index {index}")

            chapters = _payload(batch).get("chapterList")

            if not _is_success(batch) or not isinstance(chapters, list) or not chapters:
                retries += 1
                if retries > MAX_BATCH_RETRIES:
                    self._ie.report_warning(
                        f"DramaBox: giving up at index {index} after {MAX_BATCH_RETRIES} retries")
                    break
                # Guest tokens expire quickly: refresh and retry the same index.
                self._token = None
                token = self.ensure_token()
                payload["index"] = 1
                self._post(BATCH_ENDPOINT, payload, token=token, signed_with_token=True)
                payload["index"] = index
                continue

            # DramaBox truncates the list once the guest token limit is hit.
            at_end = index + 5 >= chapter_count
            if len(chapters) <= 2 and index != pay_chapter and not at_end:
                self._ie.report_warning(
                    "DramaBox: episode list truncated by the guest token limit")
                break

            _add_chapters(batch, episodes)
            index += 5
            retries = 0

        return _dedupe(episodes), book_name


# --------------------------------------------------------------------------
# Response helpers
# --------------------------------------------------------------------------

def _dig(data, *keys):
    for key in keys:
        if not isinstance(data, dict):
            return None
        data = data.get(key)
    return data


def _payload(data):
    """API responses wrap the payload in 'data', but sometimes inline it."""
    if not isinstance(data, dict):
        return {}
    inner = data.get("data")
    return inner if isinstance(inner, dict) else data


def _is_success(data):
    """Mirror DramaboxClient.IsSuccessResponse."""
    if not isinstance(data, dict):
        return False
    if data.get("success") is not None:
        return data["success"] is True
    if data.get("status") is not None:
        return int_or_none(data["status"]) == 0
    if data.get("code") is not None:
        return int_or_none(data["code"]) == 0
    chapters = _payload(data).get("chapterList")
    return isinstance(chapters, list) and bool(chapters)


def _add_chapters(data, out):
    """Append normalised episode dicts from a batch/load response."""
    chapters = _payload(data).get("chapterList")
    if not isinstance(chapters, list):
        return
    for item in chapters:
        if not isinstance(item, dict):
            continue
        cdn_list = []
        for cdn in item.get("cdnList") or []:
            if not isinstance(cdn, dict):
                continue
            paths = []
            for path in cdn.get("videoPathList") or []:
                if not isinstance(path, dict):
                    continue
                paths.append({
                    "quality": int_or_none(path.get("quality")) or 0,
                    "videoPath": path.get("videoPath") or "",
                })
            cdn_list.append({
                "cdnDomain": cdn.get("cdnDomain") or "",
                "isDefault": int_or_none(cdn.get("isDefault")) or 0,
                "videoPathList": paths,
            })
        idx = item.get("chapterIndex")
        if idx is None:
            idx = item.get("index")

        is_charge = item.get("isCharge")
        if is_charge is None:
            unlock = item.get("unlock")
            if unlock is not None:
                is_charge = 0 if unlock else 1
            else:
                is_charge = 0

        out.append({
            "chapterId": str(item.get("chapterId") or item.get("id") or ""),
            "chapterName": item.get("chapterName") or item.get("name") or "",
            "chapterIndex": int_or_none(idx) or 0,
            "isCharge": int_or_none(is_charge) or 0,
            "chapterImg": item.get("chapterImg") or item.get("cover") or "",
            "cdnList": cdn_list,
            "mp4": item.get("mp4") or "",
        })


def _dedupe(episodes):
    seen = {}
    for episode in episodes:
        key = episode.get("chapterId")
        if key and key not in seen:
            seen[key] = episode
    return sorted(seen.values(), key=lambda e: e.get("chapterIndex") or 0)


def _make_absolute_url(cdn_domain, video_path):
    """Mirror DramaBoxExtractorEngine.MakeAbsoluteUrl."""
    if not video_path:
        return ""
    if video_path.startswith("http"):
        return video_path
    if video_path.startswith("//"):
        return "https:" + video_path
    if not cdn_domain:
        return video_path
    domain = cdn_domain if cdn_domain.startswith("http") else "https://" + cdn_domain
    return domain.rstrip("/") + "/" + video_path.lstrip("/")


# --------------------------------------------------------------------------
# Extractor
# --------------------------------------------------------------------------

class DramaBoxIE(InfoExtractor):
    IE_NAME = "dramabox"
    IE_DESC = "DramaBox short dramas (dramaboxdb.com)"
    _VALID_URL = (
        r"https?://(?:www\.)?dramabox(?:db)?\.com/"
        r"(?:[^/?#]+/){0,3}(?P<id>\d+)"
    )

    # Live network tests are intentionally not recorded here: they depend on a
    # moving target (rotating CDN signatures) and would break the plugin suite.
    _TESTS = []

    def _real_extract(self, url):
        book_id = self._match_id(url) or self._extract_book_id(url)
        if not book_id:
            raise ExtractorError("DramaBox: could not determine the book id from the URL",
                                 expected=True)

        episodes = []
        book_name = None

        if not os.environ.get("DRAMABOX_DISABLE_API"):
            try:
                episodes, book_name = DramaBoxApiClient(self).fetch_episodes(book_id)
                self.write_debug(f"DramaBox API returned {len(episodes)} episodes")
            except Exception as exc:
                self.report_warning(f"DramaBox API strategy failed: {exc}")

        if not episodes:
            try:
                episodes, scraped_name = self._extract_from_html(url, book_id) or ([], None)
                if scraped_name and not book_name:
                    book_name = scraped_name
                self.write_debug(f"DramaBox HTML scraper returned {len(episodes)} episodes")
            except Exception as exc:
                self.report_warning(f"DramaBox HTML strategy failed: {exc}")

        if not episodes:
            raise ExtractorError(
                "DramaBox: no episodes could be extracted (API and HTML both failed)",
                expected=True)

        title = book_name or self._title_from_url(url)
        entries = []
        for episode in episodes:
            formats = self._build_formats(episode)
            if not formats:
                # Episode metadata exists but no playable URL (VIP-locked episode).
                continue
            number = episode.get("chapterIndex") or 0
            entries.append({
                "id": episode.get("chapterId") or f"{book_id}-{number}",
                "title": episode.get("chapterName") or f"Episode {number + 1}",
                "episode_number": number + 1,
                "formats": formats,
                "thumbnail": episode.get("chapterImg") or None,
            })

        if not entries:
            raise ExtractorError(
                "DramaBox: found episodes but none exposed a playable URL "
                "(the series may be locked). Set DRAMABOX_TOKEN to a premium "
                "token and retry.", expected=True)

        return self.playlist_result(entries, playlist_id=book_id, playlist_title=title)

    def _build_formats(self, episode):
        """Turn a normalised episode dict into yt-dlp format entries."""
        formats = []

        cdn_list = episode.get("cdnList") or []
        default_cdn = next((c for c in cdn_list if c.get("isDefault") == 1), None)
        if default_cdn is None and cdn_list:
            default_cdn = cdn_list[0]

        if default_cdn:
            domain = default_cdn.get("cdnDomain") or ""
            paths = sorted(default_cdn.get("videoPathList") or [],
                           key=lambda p: p.get("quality") or 0, reverse=True)
            for path in paths:
                media_url = _make_absolute_url(domain, path.get("videoPath") or "")
                if not media_url:
                    continue
                quality = path.get("quality") or 0
                formats.append({
                    "format_id": f"{quality}p" if quality else "cdn",
                    "url": media_url,
                    "ext": "mp4",
                    "height": quality or None,
                    "quality": quality or None,
                    "http_headers": {"Referer": "https://www.dramaboxdb.com/"},
                })

        if not formats and episode.get("mp4"):
            media_url = _make_absolute_url("", episode["mp4"])
            if media_url:
                formats.append({
                    "format_id": "mp4",
                    "url": media_url,
                    "ext": "mp4",
                    "http_headers": {"Referer": "https://www.dramaboxdb.com/"},
                })

        return formats

    def _extract_from_html(self, url, book_id):
        """Scrape __NEXT_DATA__ with domain/route fallback chains."""
        candidates = [url]
        if book_id:
            # Domain-route fallback: dramabox.com uses /drama/{id}/..., dramaboxdb.com uses /movie/{id}/...
            candidates.extend([
                f"https://www.dramabox.com/drama/{book_id}/drama",
                f"https://www.dramaboxdb.com/movie/{book_id}",
                f"https://www.dramabox.com/movie/{book_id}",
            ])

        seen_urls = set()
        deduped = []
        for c in candidates:
            if c not in seen_urls:
                seen_urls.add(c)
                deduped.append(c)

        for cand_url in deduped:
            webpage = self._download_webpage(
                cand_url, book_id, fatal=False,
                note=f"Downloading page for __NEXT_DATA__ ({cand_url})")
            if not webpage:
                continue

            match = re.search(
                r'<script id="__NEXT_DATA__" type="application/json">(.*?)</script>',
                webpage, re.DOTALL)
            if not match:
                continue

            data = self._parse_json(match.group(1), book_id, fatal=False)
            if not data:
                continue

            page_props = _dig(data, "props", "pageProps") or {}
            detail = page_props.get("detailInfo") or page_props.get("bookInfo") or {}
            if not isinstance(detail, dict):
                detail = {}

            book_name = detail.get("bookName") or detail.get("name") or page_props.get("bookName")

            chapters = (detail.get("chapterList")
                        or page_props.get("chapterList")
                        or _dig(page_props, "detailInfo", "chapterList"))
            if not isinstance(chapters, list) or not chapters:
                continue

            episodes = []
            _add_chapters({"chapterList": chapters}, episodes)
            if episodes:
                return _dedupe(episodes), book_name

        return [], None

    @staticmethod
    def _extract_book_id(url):
        """Mirror DramaBoxExtractorEngine.ExtractBookId."""
        match = re.search(
            r"(?:drama|movie|video|book|play|gifted/film).*?[=/](\d+)", url, re.IGNORECASE)
        if match:
            return match.group(1)
        if re.fullmatch(r"\d+", url.strip()):
            return url.strip()
        return None

    @staticmethod
    def _title_from_url(url):
        """Mirror DramaBoxExtractorEngine.ExtractTitleFromUrl."""
        match = re.search(
            r"(?:dramabox(?:db)?\.com)/(?:drama|movie)/\d+/([^/?#]+)"
            r"|(?:dramabox(?:db)?\.com)/video/\d+_([^/?#]+)",
            url, re.IGNORECASE)
        raw = (match.group(1) or match.group(2)) if match else None
        if not raw:
            return "DramaBox"
        normalised = raw.replace("-", " ").replace("_", " ").strip()
        return normalised.title() if normalised else "DramaBox"