/**
 * Birthday utility functions — pure client-side calculations
 * No API calls needed; all date math runs in the browser instantly.
 */

export interface BirthdayInfo {
  ageYears: number;
  ageMonths: number;
  ageDays: number;
  daysToNextBirthday: number;
}

/**
 * Compute a person's current age and days remaining until their next birthday.
 * @param dateOfBirth - ISO date string or YYYY-MM-DD format
 * @returns BirthdayInfo with exact age terms (years, months, days)
 */
export function computeBirthdayInfo(dateOfBirth: string): BirthdayInfo {
  const today = new Date();
  today.setHours(0, 0, 0, 0);

  const dob = new Date(dateOfBirth);
  dob.setHours(0, 0, 0, 0);

  if (isNaN(dob.getTime())) {
    return { ageYears: 0, ageMonths: 0, ageDays: 0, daysToNextBirthday: 0 };
  }

  // Calculate age components precisely
  let ageYears = today.getFullYear() - dob.getFullYear();
  let ageMonths = today.getMonth() - dob.getMonth();
  let ageDays = today.getDate() - dob.getDate();

  if (ageDays < 0) {
    // Borrow days from previous month
    const prevMonth = new Date(today.getFullYear(), today.getMonth(), 0);
    ageDays += prevMonth.getDate();
    ageMonths--;
  }

  if (ageMonths < 0) {
    // Borrow months from previous year
    ageMonths += 12;
    ageYears--;
  }

  // Calculate next birthday
  const nextBirthday = new Date(
    today.getFullYear(),
    dob.getMonth(),
    dob.getDate(),
  );
  nextBirthday.setHours(0, 0, 0, 0);

  // If birthday already passed this year, it's next year
  if (nextBirthday.getTime() < today.getTime()) {
    nextBirthday.setFullYear(today.getFullYear() + 1);
  }

  const msPerDay = 1000 * 60 * 60 * 24;
  const daysToNextBirthday = Math.round(
    (nextBirthday.getTime() - today.getTime()) / msPerDay,
  );

  return { ageYears, ageMonths, ageDays, daysToNextBirthday };
}
