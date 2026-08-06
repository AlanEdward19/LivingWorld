import "@testing-library/jest-dom/vitest";

// jsdom não implementa canvas 2d — StartMenu já trata ctx null (não anima em teste), mas sem
// isso o jsdom loga "Not implemented: HTMLCanvasElement.prototype.getContext" em todo teste.
HTMLCanvasElement.prototype.getContext = () => null;
