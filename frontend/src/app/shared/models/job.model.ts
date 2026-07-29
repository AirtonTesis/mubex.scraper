export enum JobStatus {
  Pending = 'Pending',
  Active = 'Active',
  Paused = 'Paused',
  Completed = 'Completed',
  Failed = 'Failed'
}

export interface Job {
  id: string;
  searchListId: string;
  status: JobStatus;
  startedAt?: Date;
  completedAt?: Date;
  retryCount: number;
  errorMessage?: string;
  createdAt: Date;
  updatedAt: Date;
}

export interface JobHistoryEntry {
  id: string;
  jobId: string;
  status: JobStatus;
  timestamp: Date;
}

export interface CreateJobRequest {
  searchListId: string;
}
