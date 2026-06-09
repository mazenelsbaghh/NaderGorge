async function globalSetup() {
  // Call the robust .NET backend hook specifically built for destroying/seeding E2e DB connections.
  const apiEndpoint = 'http://localhost:5245/api/e2e/seed';

  for (let i = 0; i < 3; i++) {
    try {
      const response = await fetch(apiEndpoint, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-E2E-Token': process.env.E2E_TEST_TOKEN || 'E2eOnlyTestTokenValue123456789012345',
        },
        body: JSON.stringify({
          clearDatabase: true,
          seedAdmin: true,
          seedStudents: true,
        }),
      });

      if (!response.ok) {
        throw new Error(
          `Failed to seed E2E Database. Status: ${response.status} - ${await response.text()}`
        );
      }

      console.log('✅ Successfully seeded NaderGorge E2E testing database.');
      return;
    } catch (e) {
      if (i === 2) {
        console.error(
          '❌ FATAL: Could not reach the API E2E seeding endpoint. Is ASPNETCORE_ENVIRONMENT=E2e running?'
        );
        throw e;
      }
      console.warn('Backend not ready yet, retrying...');
      await new Promise((r) => setTimeout(r, 2000));
    }
  }
}

export default globalSetup;
