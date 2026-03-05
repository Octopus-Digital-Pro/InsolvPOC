import client from "./client";

export interface AddressResult {
  displayName: string;
  road: string;
  houseNumber: string;
  suburb: string;
  city: string;
  county: string;
  postcode: string;
  country: string;
  lat?: string;
  lon?: string;
}

export const addressApi = {
  search: (q: string) =>
    client.get<AddressResult[]>("/address/search", { params: { q } }),
};
