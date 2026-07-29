export interface ValidationError {
  key: string;
  message?: string;
}

export interface ValidationProblemDetails {
  title: string;
  status: number;
  errors: Record<string, string[]>;
}

export interface ApiError {
  title: string;
  status: number;
  detail?: string;
  errors?: Record<string, string[]>;
}
