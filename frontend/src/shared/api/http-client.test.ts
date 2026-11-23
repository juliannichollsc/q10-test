import { describe, expect, it, vi } from "vitest";
import { ApiError, request } from "./http-client";

function stubFetch(response: Response | Error) {
  vi.stubGlobal("fetch", vi.fn(() => (response instanceof Error ? Promise.reject(response) : Promise.resolve(response))));
}

async function captureError(promise: Promise<unknown>) {
  try {
    await promise;
  } catch (error) {
    return error as ApiError;
  }
  throw new Error("Expected the request to fail.");
}

describe("request", () => {
  it("exposes validation problem field errors", async () => {
    stubFetch(Response.json(
      { title: "One or more validation errors occurred.", errors: { sku: ["Unknown SKU."] } },
      { status: 400 },
    ));

    const error = await captureError(request("/orders", { method: "POST", body: "{}" }));

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(400);
    expect(error.message).toBe("One or more validation errors occurred.");
    expect(error.fieldErrors).toEqual({ sku: ["Unknown SKU."] });
  });

  it("falls back to a readable message when the body is not JSON", async () => {
    stubFetch(new Response("Internal error", { status: 500 }));

    const error = await captureError(request("/orders"));

    expect(error.message).toBe("The request failed with status 500.");
  });

  it("reports gateway errors as a temporarily unavailable API", async () => {
    stubFetch(new Response("Bad gateway", { status: 502 }));

    const error = await captureError(request("/orders"));

    expect(error.status).toBe(502);
    expect(error.message).toMatch(/temporarily unavailable/i);
  });

  it("reports network failures as an unreachable server", async () => {
    stubFetch(new TypeError("Failed to fetch"));

    const error = await captureError(request("/orders"));

    expect(error.status).toBe(0);
    expect(error.message).toMatch(/cannot reach the server/i);
  });
});
