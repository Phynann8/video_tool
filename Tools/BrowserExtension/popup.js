const NATIVE_HOST = "com.universalmediadownloader.nativehost";

document.getElementById("downloadBtn").addEventListener("click", async () => {
  const statusEl = document.getElementById("status");
  statusEl.textContent = "Connecting to desktop app...";
  statusEl.className = "";

  try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tab || !tab.url) {
      statusEl.textContent = "No active tab found.";
      statusEl.className = "status-err";
      return;
    }

    let cookieHeader = "";
    try {
      const cookies = await chrome.cookies.getAll({ url: tab.url });
      if (cookies && cookies.length > 0) {
        cookieHeader = cookies.map(c => `${c.name}=${c.value}`).join("; ");
      }
    } catch (e) {
      console.warn("Could not query cookies:", e);
    }

    const payload = {
      action: "download",
      url: tab.url,
      pageUrl: tab.url,
      pageTitle: tab.title || "",
      cookies: cookieHeader,
      userAgent: navigator.userAgent
    };

    chrome.runtime.sendNativeMessage(NATIVE_HOST, payload, response => {
      if (chrome.runtime.lastError) {
        statusEl.textContent = "Error: " + chrome.runtime.lastError.message;
        statusEl.className = "status-err";
      } else if (response && response.status === "ok") {
        statusEl.textContent = "Sent to Universal Media Downloader!";
        statusEl.className = "status-ok";
      } else {
        statusEl.textContent = response ? response.message : "Unknown error";
        statusEl.className = "status-err";
      }
    });
  } catch (err) {
    statusEl.textContent = "Failed: " + err.message;
    statusEl.className = "status-err";
  }
});
