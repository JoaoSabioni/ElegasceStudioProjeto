const menuButton = document.querySelector("[data-menu-button]");
const mobileMenu = document.querySelector("[data-mobile-menu]");
const currentYear = new Date().getFullYear().toString();
const pageLoader = document.querySelector("[data-page-loader]");

document.querySelectorAll("[data-current-year]").forEach((element) => {
  element.textContent = currentYear;
});

if (pageLoader) {
  document.body.classList.add("loading");
  window.addEventListener("load", () => {
    window.setTimeout(() => {
      pageLoader.classList.add("hide");
      document.body.classList.remove("loading");
    }, 420);
  });
}

if (menuButton && mobileMenu) {
  menuButton.addEventListener("click", () => {
    mobileMenu.classList.toggle("open");
  });
}

const lightbox = document.querySelector("[data-lightbox]");
const lightboxImage = document.querySelector("[data-lightbox-image]");
const lightboxClose = document.querySelector("[data-lightbox-close]");
const lightboxPrev = document.querySelector("[data-lightbox-prev]");
const lightboxNext = document.querySelector("[data-lightbox-next]");
const galleryButtons = Array.from(document.querySelectorAll("[data-gallery-image]"));
let currentGallery = [];
let currentGalleryIndex = 0;

function setLightboxImage(index) {
  if (!lightboxImage || currentGallery.length === 0) return;
  currentGalleryIndex = (index + currentGallery.length) % currentGallery.length;
  lightboxImage.setAttribute("src", currentGallery[currentGalleryIndex]);
}

function moveLightbox(step) {
  if (!lightbox || !lightbox.classList.contains("open")) return;
  setLightboxImage(currentGalleryIndex + step);
}

galleryButtons.forEach((button) => {
  button.addEventListener("click", () => {
    const src = button.getAttribute("data-gallery-image");
    const group = button.getAttribute("data-gallery-group");
    if (!src || !lightbox || !lightboxImage) return;
    currentGallery = galleryButtons
      .filter((item) => item.getAttribute("data-gallery-group") === group)
      .map((item) => item.getAttribute("data-gallery-image"))
      .filter(Boolean);
    if (currentGallery.length === 0) currentGallery = [src];
    setLightboxImage(currentGallery.indexOf(src));
    lightbox.classList.add("open");
  });
});

function closeLightbox() {
  if (lightbox) lightbox.classList.remove("open");
}

if (lightboxClose) lightboxClose.addEventListener("click", closeLightbox);
if (lightboxPrev) lightboxPrev.addEventListener("click", () => moveLightbox(-1));
if (lightboxNext) lightboxNext.addEventListener("click", () => moveLightbox(1));
if (lightbox) {
  lightbox.addEventListener("click", (event) => {
    if (event.target === lightbox) closeLightbox();
  });
}

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape") closeLightbox();
  if (event.key === "ArrowLeft") moveLightbox(-1);
  if (event.key === "ArrowRight") moveLightbox(1);
});
