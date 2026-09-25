# Source Reference — Family Solar Manual

Original source supplied by the user on 2026-09-25.

- filename: `Manual_Familiar_SPRO_6200_LC230_Midea_v2_ES.docx`
- manual title: *Energía Familiar — Manual familiar del sistema solar*
- edition: v2.0
- manual date: 2026-08-14
- size: 546,109 bytes
- SHA-256: `f519a39be14950258ce51d3cbb3a7e69cbc6b23769b2ae9e47c77ca71f5a3bde`

Development-relevant content has been integrated into:

- `../INSTALLATION_BEHAVIOR_CONTRACT.md`
- `../MANUAL_FAMILIAR_INTEGRATION_REVIEW_2026-09-25.md`
- `../PRODUCT_FUNCTIONAL_SPEC_V1.md`
- `../DEVELOPMENT_ROADMAP_V1.md`

User confirmation at integration time:

- the manual's recommended inverter changes are currently applied.

Important source caveat from the manual itself:

- exact SPRO firmware version remains unconfirmed;
- exact CT / zero-export meter model and wiring remain unconfirmed;
- physical use of the second AC output remains unconfirmed;
- PV orientation/inclination/shading/string layout remain unconfirmed.

The Windows application must therefore combine this installation-specific reference with live read-only SiSeLi evidence and preserve uncertainty when they differ.

## Binary-source handling

The exact DOCX is the upstream source supplied in the development conversation.

The available GitHub connector in this environment supports UTF-8 repository writes but does not provide a direct arbitrary-binary upload handoff from the attached local file. The binary is therefore identified here by exact filename, size and SHA-256 rather than pretending it was committed.

If the exact DOCX is later added manually to the repository, place it at:

`solar_of_things_windows_app/reference/Manual_Familiar_SPRO_6200_LC230_Midea_v2_ES.docx`

and verify the SHA-256 above.
