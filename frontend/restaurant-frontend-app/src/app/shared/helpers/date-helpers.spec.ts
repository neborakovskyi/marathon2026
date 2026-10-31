import {
  addAndRoundBookingTime,
  addMinutesIso,
  combineDateAndTime,
  findClosestDuration,
  formatDate,
  formatTime,
  generateDateOptions,
  generateTimeOptions,
  minutesBetween,
  parseToISO,
  revertFormatDate,
  revertFormatTime,
} from './date-helpers';

describe('date-helpers', () => {
  beforeEach(() => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date(2026, 9, 17, 12, 0));
  });

  afterEach(() => jasmine.clock().uninstall());

  it('formats and reverts dates and times', () => {
    expect(formatTime('2026-10-17T09:05:00')).toBe('09:05');
    expect(formatDate('2026-10-17T12:00:00')).toBe('October 17, 2026');

    const afternoon = new Date(revertFormatTime('2:15 p.', '2026-10-17T00:00:00'));
    expect(afternoon.getHours()).toBe(14);
    expect(afternoon.getMinutes()).toBe(15);
    const midnight = new Date(revertFormatTime('12:30 a.', '2026-10-17T00:00:00'));
    expect(midnight.getHours()).toBe(0);

    const formatted = revertFormatDate('2026-10-17T09:05:00');
    expect(formatted).toBe('2026-10-17T09:05');
  });

  it('generates the next two weeks of dates and advances after the late cutoff', () => {
    jasmine.clock().mockDate(new Date(2026, 9, 17, 21, 16));

    const options = generateDateOptions();

    expect(options).toHaveSize(14);
    expect(options[0]).toBe(new Intl.DateTimeFormat('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    }).format(new Date(2026, 9, 18, 21, 16)));
  });

  it('generates all available slots on a different day and rounds today forward', () => {
    jasmine.clock().mockDate(new Date(2026, 9, 17, 12, 20));

    const anotherDay = generateTimeOptions('2026-10-18T00:00:00');
    expect(anotherDay[0]).toBe('10:00');
    expect(anotherDay[anotherDay.length - 1]).toBe('21:30');
    expect(generateTimeOptions('2026-10-17T00:00:00')[0]).toBe('13:00');

    jasmine.clock().mockDate(new Date(2026, 9, 17, 21, 50));
    expect(generateTimeOptions('2026-10-17T00:00:00')).toEqual([]);
  });

  it('combines date and time and supports both AM and PM input', () => {
    expect(combineDateAndTime('2026-10-17T00:00:00', '1970-01-01T14:30:00Z')).toBe(
      '2026-10-17T14:30:00.000Z'
    );
    expect(new Date(parseToISO('2026-10-17T00:00:00', '2:30 p.')).getHours()).toBe(14);
    expect(new Date(parseToISO('2026-10-17T00:00:00', '12:30 a.')).getHours()).toBe(0);
    expect(parseToISO('2026-10-17T00:00:00', '14:30')).toBe(
      new Date(2026, 9, 17, 14, 30).toISOString()
    );
  });

  it('adds and rounds booking time in 15-minute increments', () => {
    expect(addAndRoundBookingTime('2026-10-17T12:07:00.000Z')).toBe(
      '2026-10-17T12:30:00.000Z'
    );
    expect(addAndRoundBookingTime('2026-10-17T12:50:00.000Z', 15, 30)).toBe(
      '2026-10-17T13:30:00.000Z'
    );
  });

  describe('addMinutesIso', () => {
    const cases: { name: string; input: string; minutes: number; expected: string }[] = [
      {
        name: 'adds minutes within the same hour',
        input: '2026-10-17T12:00:00.000Z',
        minutes: 30,
        expected: '2026-10-17T12:30:00.000Z',
      },
      {
        name: 'adds several hours',
        input: '2026-10-17T12:00:00.000Z',
        minutes: 180,
        expected: '2026-10-17T15:00:00.000Z',
      },
      {
        name: 'crosses midnight into the next day',
        input: '2026-10-17T23:30:00.000Z',
        minutes: 90,
        expected: '2026-10-18T01:00:00.000Z',
      },
      {
        name: 'returns the same instant for zero minutes',
        input: '2026-10-17T12:00:00.000Z',
        minutes: 0,
        expected: '2026-10-17T12:00:00.000Z',
      },
      {
        name: 'accepts ISO strings without milliseconds',
        input: '2026-10-17T12:00:00Z',
        minutes: 120,
        expected: '2026-10-17T14:00:00.000Z',
      },
    ];

    cases.forEach(({ name, input, minutes, expected }) => {
      it(name, () => {
        expect(addMinutesIso(input, minutes)).toBe(expected);
      });
    });
  });

  describe('minutesBetween', () => {
    it('returns the length of a 90-minute slot', () => {
      expect(minutesBetween('2026-10-17T12:00:00Z', '2026-10-17T13:30:00Z')).toBe(90);
    });

    it('counts across midnight', () => {
      expect(minutesBetween('2026-10-17T23:00:00Z', '2026-10-18T01:00:00Z')).toBe(120);
    });

    it('returns zero for the same instant', () => {
      expect(minutesBetween('2026-10-17T12:00:00Z', '2026-10-17T12:00:00Z')).toBe(0);
    });
  });

  describe('findClosestDuration', () => {
    const durations = [90, 120, 150, 180];
    const cases: { name: string; minutes: number; expected: number }[] = [
      { name: 'returns the exact match', minutes: 120, expected: 120 },
      { name: 'rounds down to the nearest option', minutes: 130, expected: 120 },
      { name: 'rounds up to the nearest option', minutes: 145, expected: 150 },
      { name: 'picks the shorter option on a tie', minutes: 135, expected: 120 },
      { name: 'clamps below the shortest option', minutes: 30, expected: 90 },
      { name: 'clamps above the longest option', minutes: 600, expected: 180 },
      { name: 'handles zero minutes', minutes: 0, expected: 90 },
    ];

    cases.forEach(({ name, minutes, expected }) => {
      it(name, () => {
        expect(findClosestDuration(minutes, durations)).toBe(expected);
      });
    });

    it('returns the only option when there is one', () => {
      expect(findClosestDuration(500, [60])).toBe(60);
    });
  });
});
