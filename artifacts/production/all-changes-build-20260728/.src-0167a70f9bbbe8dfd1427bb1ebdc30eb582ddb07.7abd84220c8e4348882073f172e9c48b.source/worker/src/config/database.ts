const developmentDatabaseUrl =
  'postgresql://postgres:postgres@localhost:5432/nadergorge?schema=public';

export function databaseUrl() {
  const configured = process.env.DATABASE_URL || process.env.DB_CONNECTION_STRING;
  if (configured) return configured;
  if (process.env.NODE_ENV === 'production') {
    throw new Error('DATABASE_URL is required in production.');
  }
  return developmentDatabaseUrl;
}
