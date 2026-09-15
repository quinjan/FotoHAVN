export const siteBasePath = "";
export const siteUrl = "https://fotohavn.com";
export const instagramMessageUrl = "https://ig.me/m/fotohavn.ph";
export const inquirySectionId = "make-something-worth-keeping";
export const findBoothSectionHash = "#find-the-booth";
export const rentFotohavnSectionHash = "#rent-fotohavn";

export function withSiteBasePath(path: `/${string}`) {
  return `${siteBasePath}${path}`;
}

export const findBoothSectionUrl = withSiteBasePath(`/${findBoothSectionHash}`);
