import moment, { Moment } from 'moment-timezone';
import { CalendarStatus } from 'typings/Calendar';

function getStatusStyle(
  hasFile: boolean,
  downloading: boolean,
  startTime: Moment | undefined,
  endTime: Moment | undefined,
  isMonitored: boolean,
  inReview = false
): CalendarStatus {
  const currentTime = moment();

  if (downloading) {
    return 'downloading';
  }

  if (inReview) {
    return 'inReview';
  }

  if (hasFile) {
    return 'downloaded';
  }

  if (!isMonitored) {
    return 'unmonitored';
  }

  if (!startTime || !endTime) {
    return 'unaired';
  }

  if (currentTime.isAfter(startTime) && currentTime.isBefore(endTime)) {
    return 'onAir';
  }

  if (endTime.isBefore(currentTime) && !hasFile) {
    return 'missing';
  }

  return 'unaired';
}

export default getStatusStyle;
