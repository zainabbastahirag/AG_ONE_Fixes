window.LNK = {
  showToast(message, type = "success") {
    let container = document.querySelector(".lnk-toast-container");
    if (!container) {
      container = document.createElement("div");
      container.className = "lnk-toast-container";
      document.body.appendChild(container);
    }
    const toast = document.createElement("div");
    toast.className = "lnk-toast";
    toast.innerHTML = `<strong>${type === "success" ? "✓" : "!"}</strong> ${message}`;
    container.appendChild(toast);
    setTimeout(() => toast.remove(), 4000);
  },

  async copyText(text) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      const ta = document.createElement("textarea");
      ta.value = text;
      document.body.appendChild(ta);
      ta.select();
      document.execCommand("copy");
      ta.remove();
      return true;
    }
  },

  async copyAndOpenLinkedIn(text) {
    await this.copyText(text);
    window.open("https://www.linkedin.com/feed/", "_blank");
    this.showToast("Post copied! LinkedIn opened in a new tab.");
  }
};

document.addEventListener("DOMContentLoaded", () => {
  const nav = document.querySelector(".lnk-nav");
  if (nav) {
    window.addEventListener("scroll", () => {
      nav.classList.toggle("scrolled", window.scrollY > 40);
    });
  }

  const toastMsg = document.body.dataset.toast;
  if (toastMsg) LNK.showToast(toastMsg);

  document.querySelectorAll("[data-copy]").forEach((btn) => {
    btn.addEventListener("click", async () => {
      const text = btn.dataset.copy || "";
      await LNK.copyText(text);
      LNK.showToast("Copied to clipboard!");
    });
  });

  document.querySelectorAll("[data-copy-linkedin]").forEach((btn) => {
    btn.addEventListener("click", async () => {
      await LNK.copyAndOpenLinkedIn(btn.dataset.copyLinkedin || "");
    });
  });
});
