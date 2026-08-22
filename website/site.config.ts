export const siteBasePath = "/fotohvn";
export const stagingSiteUrl = `http://159.223.47.227${siteBasePath}`;

export function withSiteBasePath(path: `/${string}`) {
  return `${siteBasePath}${path}`;
}
