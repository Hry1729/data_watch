import { addCollection } from '@iconify/vue/offline'
import remixIcons from '@iconify-json/ri/icons.json'
import nonRiIconCollections from './iconify-non-ri-subset.json'

type IconifyCollection = Parameters<typeof addCollection>[0]

addCollection(remixIcons)

;(nonRiIconCollections as unknown as IconifyCollection[]).forEach((collection) => {
  addCollection(collection)
})
