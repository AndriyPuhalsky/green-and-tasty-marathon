import { GroupReservationStatus } from '@app/models/group-orders/group-status.model';

export const SYNC_INTERVAL_MS = 15_000;

export const GROUP_STATUS_LABELS: Record<GroupReservationStatus, string> = {
  Open: 'Collecting pre-orders',
  Submitted: 'Sent to Restaurant',
  Closed: 'Closed',
};
