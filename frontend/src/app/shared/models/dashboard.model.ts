export interface DashboardMetrics {
  totalSearches: number;
  successRate: number;
  failureRate: number;
  activeExecutions: number;
}

export enum TemporalFilter {
  Day = 'day',
  Week = 'week',
  Month = 'month',
  Custom = 'custom'
}

export interface DateRange {
  startDate: Date;
  endDate: Date;
}
