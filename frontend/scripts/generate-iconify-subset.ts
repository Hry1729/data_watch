import { readFile, writeFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

type IconifyCollection = {
  prefix: string
  width?: number
  height?: number
  icons: Record<string, unknown>
  aliases?: Record<string, { parent: string } & Record<string, unknown>>
}

type IconRequest = {
  packageName: string
  icons: string[]
}

const scriptDir = dirname(fileURLToPath(import.meta.url))
const frontendRoot = resolve(scriptDir, '..')
const outputPath = resolve(frontendRoot, 'src/utils/ui/iconify-non-ri-subset.json')

const requests: IconRequest[] = [
  { packageName: 'fluent', icons: ['arrow-enter-left-20-filled'] },
  { packageName: 'icon-park-outline', icons: ['auto-width'] },
  { packageName: 'iconamoon', icons: ['arrow-down-2-thin'] },
  { packageName: 'solar', icons: ['double-alt-arrow-right-linear'] },
  { packageName: 'system-uicons', icons: ['inbox'] },
  { packageName: 'vaadin', icons: ['ctrl-a'] }
]

const readCollection = async (packageName: string) => {
  const filePath = resolve(frontendRoot, `node_modules/@iconify-json/${packageName}/icons.json`)
  return JSON.parse(await readFile(filePath, 'utf8')) as IconifyCollection
}

const pickIcons = (source: IconifyCollection, names: string[]) => {
  const subset: IconifyCollection = {
    prefix: source.prefix,
    icons: {}
  }

  if (source.width !== undefined) subset.width = source.width
  if (source.height !== undefined) subset.height = source.height

  const includeIcon = (name: string) => {
    if (source.icons[name]) {
      subset.icons[name] = source.icons[name]
      return
    }

    const alias = source.aliases?.[name]
    if (alias) {
      subset.aliases ||= {}
      subset.aliases[name] = alias
      includeIcon(alias.parent)
      return
    }

    throw new Error(`Icon "${source.prefix}:${name}" was not found.`)
  }

  names.forEach(includeIcon)

  return subset
}

const collections = await Promise.all(
  requests.map(async (request) => {
    const collection = await readCollection(request.packageName)
    return pickIcons(collection, request.icons)
  })
)

await writeFile(outputPath, `${JSON.stringify(collections, null, 2)}\n`, 'utf8')
