---
title: 'PTCalc: open computational tools for pharmaceutical technology'
tags:
  - pharmaceutical technology
  - dissolution kinetics
  - drug release modelling
  - model selection
  - pseudo-ternary phase diagram
  - analytical method validation
  - C#
  - .NET
authors:
  - name: Mehmet Ali Ege
    orcid: 0000-0002-4953-2812
    affiliation: 1
affiliations:
  - name: Department of Pharmaceutical Technology, Faculty of Pharmacy, Ege University, İzmir, Türkiye
    index: 1
date: 22 September 2026
bibliography: paper.bib
---

# Summary

PTCalc ("Pharmaceutical Technology Calculators") is an open-source web application and .NET library for the
routine computations of pharmaceutical technology and formulation research. Its core is a dissolution-kinetics
engine that fits sixteen drug-release models (zero-order, first-order, Higuchi, Hixson–Crowell,
Korsmeyer–Peppas, Weibull, Hopfenberg, Baker–Lonsdale, Makoid–Banakar, Peppas–Sahlin, logistic, Gompertz,
probit and their $F_\mathrm{max}$ forms) by nonlinear least squares in the released-fraction domain, with
optional lag-time ($T_\mathrm{lag}$), burst ($F_0$) and plateau ($F_\mathrm{max}$) variants, and ranks them by
the Akaike information criterion with Akaike weights [@Akaike1974]. Around the engine sit tools that
formulation scientists use daily: $f_1$/$f_2$ similarity factors and model-independent profile metrics
[@Moore1996; @Costa2001], a pseudo-ternary phase-diagram tool that measures the area and centroid of
emulsion/microemulsion regions, dose–response analysis (LD$_{50}$/LD$_{90}$ by probit, logit and 4PL), and a
statistics set for laboratory data: $t$-test, one-way ANOVA with Tukey HSD [@Tukey1949; @Kramer1956], multiple
linear regression with prediction intervals, and calibration curves with LOD/LOQ following ICH Q2(R2)
[@ICHQ2R2; @Miller2018].

The application runs in the browser (Blazor Server, .NET 10) at <https://ptcalc.net> (English) and
<https://ptcalc.tr> (Turkish); data entered by the user stays in the browser session and is never stored on the
server. The same computations are available as a dependency-light class library (`PTCalc.Core`, MIT licence)
that can be used from any .NET language or from scripts. Each tool ships with a bilingual guide that explains
the method, its prerequisites and its common misuses, with DOI-referenced sources.

# Statement of need

Dissolution-profile modelling is a standard step in formulation development and in *in vitro*–*in vivo*
correlation studies, yet it is still mostly done with spreadsheets. The most widely used dedicated tool,
DDSolver [@Zhang2010], is an Excel add-in restricted to Windows Excel, and KinetDS [@Mendyk2012] is a Java
desktop program that is no longer maintained. Spreadsheet practice commonly linearises each model (for example
$\log F$ versus $\log t$ for Korsmeyer–Peppas) and compares models by $R^2$ in the transformed space, which
makes the goodness-of-fit measures incomparable across models and systematically favours models with more
parameters. PTCalc addresses these problems in one place:

- every model is fitted to the same data in the same ($F$, %) domain, so residual sums of squares, AIC, AICc
  and MSC are directly comparable, and the ranking penalises extra parameters;
- the Korsmeyer–Peppas rule of fitting only the $F \le 60\,\%$ portion [@Korsmeyer1983; @Costa2001] is applied
  by default and reported separately, because a model fitted to a subset of the points cannot be ranked by AIC
  against models fitted to all points;
- Hopfenberg geometry exponents [@Hopfenberg1976] include the half-sphere and triangle cases introduced for
  erodible tablets by @Karasulu2000, and degenerate cases that coincide with other models are excluded from the
  ranking so that Akaike weights are not double-counted;
- the solver combines a linear seed with Levenberg–Marquardt refinement and a Nelder–Mead fallback and never
  returns a fit worse than its seed.

The engine's equations, point-selection rules and goodness-of-fit definitions were compared with DDSolver 1.0
on 32 worksheets (37 formulations) generated with the add-in's own example data: at DDSolver's parameter
estimates PTCalc reproduces DDSolver's SS, $R^2$, adjusted $R^2$, MSE, AIC, MSC and secondary parameters
($T_{25}$–$T_{90}$) to within $10^{-7}$ relative error, and its own fits are never worse in SS than
DDSolver's in any of the 37 cases. These comparisons are part of the test suite and run in continuous
integration. The statistics module is compared in the same way against SciPy and statsmodels reference values,
including the studentized-range distribution used by Tukey HSD, which is implemented from its integral
definition because no .NET numerical library provides it.

The pseudo-ternary phase-diagram tool computes the area and centroid of a phase region by the shoelace formula
on the plotted polygon, a metric the author's group has used since 2001 to compare surfactant systems
quantitatively rather than by visual inspection, and draws the region boundary as a centripetal Catmull–Rom
curve through the measured points. The calibration-curve tool is designed for teaching as well as routine use:
besides slope, intercept, LOD and LOQ it back-calculates every standard, flags levels outside the usual
accuracy limits, and shows the inverse-prediction calculation for unknown samples step by step with the
intermediate quantities [@Miller2018].

PTCalc is intended for formulation scientists, analytical laboratories and pharmacy students. The tools descend
from desktop programs developed and used in the author's department since 1995; the present repository is a
rewrite as a single open-source web application and library.

# Acknowledgements

The author thanks colleagues at the Department of Pharmaceutical Technology, Ege University, whose dissolution
and phase-diagram data shaped these tools over many years. DDSolver example data are used only within the unit
tests for comparison; see the repository's third-party notices.

# References
