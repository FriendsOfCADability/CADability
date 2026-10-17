// Starts the smoke test under Node.js: node bin/<configuration>/net10.0/wwwroot/main.mjs (the build copies this file there)
import { dotnet } from './_framework/dotnet.js'

const exitCode = await dotnet.run();
process.exit(exitCode);
