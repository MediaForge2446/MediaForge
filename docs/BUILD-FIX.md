# Build fix

The Core project generates XML documentation while warnings are treated as errors globally. CS1591 is excluded for the Core project because its public API is intentionally documented incrementally without breaking the build pipeline.
