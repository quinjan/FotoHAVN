export const siteBasePath = "/fotohavn";
export const stagingSiteUrl = `http://159.223.47.227${siteBasePath}`;
export const instagramMessageUrl = "https://ig.me/m/fotohavn.ph";
export const inquirySectionId = "make-something-worth-keeping";
export const findBoothSectionHash = "#find-the-booth";
export const rentFotohavnSectionHash = "#rent-fotohavn";

export function withSiteBasePath(path: `/${string}`) {
  return `${siteBasePath}${path}`;
}

export const findBoothSectionUrl = withSiteBasePath(`/${findBoothSectionHash}`);
