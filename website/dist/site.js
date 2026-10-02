const repo = "TH3AL3X/Pinguin-Anti-Scan";

fetch(`https://api.github.com/repos/${repo}/releases/latest`, {
  headers: { Accept: "application/vnd.github+json" }
})
  .then((response) => {
    if (!response.ok) throw new Error("Release metadata unavailable");
    return response.json();
  })
  .then((release) => {
    const asset = release.assets?.find((item) => item.name === "PenguinAntiScan.exe");
    document.querySelectorAll("[data-version]").forEach((element) => {
      element.textContent = release.tag_name ? `Latest: ${release.tag_name}` : "Latest release";
    });
    if (asset?.browser_download_url) {
      document.querySelectorAll("[data-download]").forEach((link) => {
        link.href = asset.browser_download_url;
      });
    }
  })
  .catch(() => {
    // The stable /releases/latest/download URL remains valid without API metadata.
  });
