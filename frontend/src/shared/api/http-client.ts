export type FieldErrors = Record<string, string[]>;

type ProblemDetails = {
  title?: string;
  detail?: string;
  errors?: FieldErrors;
};

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly fieldErrors: FieldErrors = {},
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  if (init.body && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");

  let response: Response;
  try {
    response = await fetch(`/api${path}`, { ...init, headers });
  } catch {
    throw new ApiError("Cannot reach the server. Check that the system is running and try again.", 0);
  }

  if (response.status >= 502 && response.status <= 504) {
    throw new ApiError("The Orders API is temporarily unavailable. Retrying automatically.", response.status);
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as ProblemDetails | null;
    throw new ApiError(
      problem?.detail || problem?.title || `The request failed with status ${response.status}.`,
      response.status,
      problem?.errors ?? {},
    );
  }

  return (await response.json()) as T;
}
