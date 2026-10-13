# Copilot Instructions

## Project Guidelines
- For API key validation, narrow lookup by ProjectId + KeyPrefix before comparing hashed secret.
- API keys are used only for publish-event requests; dashboard requests use JWT authentication.