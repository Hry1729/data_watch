let fileSaverModulePromise: Promise<typeof import('file-saver')> | null = null

const loadFileSaver = () => {
  if (!fileSaverModulePromise) {
    fileSaverModulePromise = import('file-saver')
  }

  return fileSaverModulePromise
}

export const saveBlobAs = async (blob: Blob, filename: string) => {
  const fileSaverModule = await loadFileSaver()
  fileSaverModule.default.saveAs(blob, filename)
}
