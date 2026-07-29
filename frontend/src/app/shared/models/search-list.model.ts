export interface SearchList {
  id: string;
  name: string;
  keywords: string[];
  domains: string[];
  userId: string;
  createdAt: Date;
  updatedAt: Date;
}

export interface CreateSearchListRequest {
  name: string;
  keywords: string[];
  domains: string[];
}

export interface UpdateSearchListRequest {
  name: string;
  keywords: string[];
  domains: string[];
}
