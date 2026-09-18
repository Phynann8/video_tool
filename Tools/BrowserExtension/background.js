const NATIVE_HOST = "com.universalmediadownloader.nativehost";

chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({
    id: "umd-download-target",
    title: "Download with Universal Media Downloader",
    contexts: ["link", "video", "audio", "page"]
  });
});

chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  if (info.menuItemId === "umd-download-target") {
    const targetUrl = info.srcUrl || info.linkUrl || info.pageUrl || (tab && tab.url);
    if (!targetUrl) return;

    await sendDownloadToNativeHost(targetUrl, tab);
  }
});

async function sendDownloadToNativeHost(targetUrl, tab) {
  let cookieHeader = "";

  try {
    const cookies = await chrome.cookies.getAll({ url: targetUrl });
    if (cookies && cookies.length > 0) {
      cookieHeader = cookies.map(c => `${c.name}=${c.value}`).join("; ");
    }
  } catch (err) {
    console.warn("Could not extract cookies:", err);
  }

  const payload = {
    action: "download",
    url: targetUrl,
    pageUrl: tab ? tab.url : targetUrl,
    pageTitle: tab ? tab.title : "",
    cookies: cookieHeader,
    userAgent: navigator.userAgent
  };

  try {
    chrome.runtime.sendNativeMessage(NATIVE_HOST, payload, response => {
      if (chrome.runtime.lastError) {
        console.error("Native messaging error:", chrome.runtime.lastError.message);
      } else {
        console.log("Native host response:", response);
      }
    });
  } catch (err) {
    console.error("Failed to communicate with native host:", err);
  }
}
