/**
 * 配方管理 — 保存/加载参数组到 PLC
 */

import fs from 'fs/promises'

export interface Recipe {
  name: string
  description?: string
  values: Record<string, number>
  createdAt: number
  updatedAt: number
}

let recipes: Recipe[] = []

const DATA_FILE = import.meta.dirname + '/../data/recipes.json'
try {
  const raw = await fs.readFile(DATA_FILE, 'utf-8')
  recipes = JSON.parse(raw)
} catch {}

async function save() {
  try { await fs.writeFile(DATA_FILE, JSON.stringify(recipes, null, 2), 'utf-8') } catch {}
}

export function getRecipes(): Recipe[] { return recipes }
export function getRecipe(name: string): Recipe | undefined { return recipes.find(r => r.name === name) }

export async function createRecipe(name: string, values: Record<string, number>, description?: string): Promise<Recipe> {
  if (recipes.find(r => r.name === name)) throw new Error(`配方 "${name}" 已存在`)
  const recipe: Recipe = { name, description, values, createdAt: Date.now(), updatedAt: Date.now() }
  recipes.push(recipe)
  await save()
  return recipe
}

export async function updateRecipe(name: string, values: Record<string, number>, description?: string): Promise<Recipe> {
  const recipe = recipes.find(r => r.name === name)
  if (!recipe) throw new Error(`配方 "${name}" 不存在`)
  recipe.values = values
  if (description !== undefined) recipe.description = description
  recipe.updatedAt = Date.now()
  await save()
  return recipe
}

export async function deleteRecipe(name: string): Promise<void> {
  recipes = recipes.filter(r => r.name !== name)
  await save()
}
