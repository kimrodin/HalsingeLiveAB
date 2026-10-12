export const ALLOWED_SEATING_MAP_TYPES = ['image/png', 'image/jpeg', 'application/pdf'];
export const MAX_SEATING_MAP_BYTES = 5 * 1024 * 1024; // 5 MB

export function validateSeatingMapFile(file: File): string | null {
  if (!ALLOWED_SEATING_MAP_TYPES.includes(file.type)) {
    return 'Filen måste vara PNG, JPG eller PDF';
  }
  if (file.size > MAX_SEATING_MAP_BYTES) {
    return 'Filen får vara max 5 MB';
  }
  return null;
}