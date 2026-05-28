# mdev_project

Travel itinerary planning MVP for families doing basecamp-style trips in Europe.

## Overview

This project is designed to generate realistic, day-by-day travel itineraries organized into **Morning / Afternoon / Night** blocks. The goal is to help families maximize sightseeing while keeping plans practical, adaptable, and easy to follow.

The product focuses on:

- prioritizing must-see places
- grouping visits into executable daily blocks
- suggesting sensible transport options
- adapting plans to weather changes
- supporting lightweight replanning during the day

## Product concept

The app combines deterministic planning logic with AI-assisted explanations and external data integrations.

Core idea:

- **Planner** = operational brain that builds and validates itineraries
- **Tools / APIs** = access to live external facts such as places, routes, and weather
- **LLM** = intelligent conversational layer for explanations, recommendations, and free-form questions
- **Backend** = source of truth and final validator

A key architectural principle is that the LLM should not directly write the final itinerary. Instead, itinerary generation and validation stay in the planner/backend layer to keep results deterministic, auditable, and robust.

## Main MVP features

- Create a trip with city, dates, and base location
- Add must-see places with priority
- Generate a block-based itinerary for each day
- Show estimated duration per block and visit
- Suggest transport mode between visits
- Support indoor/outdoor swaps for bad weather
- Allow checklist tracking and manual replanning

## Architecture summary

The repository documentation describes a hybrid architecture with:

- client apps for trip setup, itinerary viewing, and replanning
- a Java Spring Boot backend organized as a modular monolith
- a deterministic planner / rule engine
- an AI and tool orchestration layer
- PostgreSQL as the operational data layer
- curated product knowledge for planning heuristics and enriched place data
- external APIs for maps, routing, weather, and place information

This approach keeps critical planning logic under system control while still using AI where it adds the most value: explanation, orchestration, and intelligent assistance.

## Documentation

More detailed project information is available in:

- `app_arch.md` — conceptual architecture and system layering
- `spec-v2.md` — MVP product specification and planning rules
