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
      element.textContent = release.tag_name ? `Latest · ${release.tag_name}` : "Latest release";
    });
    if (release.published_at) {
      const date = new Intl.DateTimeFormat("en", { month: "short", day: "numeric", year: "numeric" }).format(new Date(release.published_at));
      document.querySelectorAll("[data-release-date]").forEach((element) => {
        element.textContent = `Released ${date}`;
      });
    }
    if (asset?.browser_download_url) {
      document.querySelectorAll("[data-download]").forEach((link) => {
        link.href = asset.browser_download_url;
      });
    }
  })
  .catch(() => {
    // The stable /releases/latest/download URL remains valid without API metadata.
  });

const demo = document.querySelector("[data-demo-state]");
const demoToggle = document.querySelector("[data-demo-toggle]");
if (demo && demoToggle) {
  demoToggle.addEventListener("click", () => {
    const protectedMode = demo.dataset.demoState !== "protected";
    demo.dataset.demoState = protectedMode ? "protected" : "scanning";
    demoToggle.setAttribute("aria-pressed", String(protectedMode));
    demo.querySelector("[data-demo-label]").textContent = protectedMode ? "Blocked" : "Allowed";
    demo.querySelector("[data-demo-detail]").textContent = protectedMode ? "Penguin is on guard" : "Windows may scan periodically";
  });
}

const header = document.querySelector("[data-header]");
const updateHeader = () => header?.classList.toggle("is-scrolled", window.scrollY > 24);
updateHeader();
window.addEventListener("scroll", updateHeader, { passive: true });

const reveals = document.querySelectorAll(".reveal");
if ("IntersectionObserver" in window && !window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
  const observer = new IntersectionObserver((entries) => {
    entries.forEach((entry) => {
      if (entry.isIntersecting) {
        entry.target.classList.add("is-visible");
        observer.unobserve(entry.target);
      }
    });
  }, { threshold: 0.12 });
  reveals.forEach((element) => observer.observe(element));
} else {
  reveals.forEach((element) => element.classList.add("is-visible"));
}
