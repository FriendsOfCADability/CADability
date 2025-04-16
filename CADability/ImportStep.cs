using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using MathNet.Numerics.LinearAlgebra.Double;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wintellect.PowerCollections;
using System.Threading;
using System.Diagnostics;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability
{
	/* Links:
     * http://www.buildingsmart-tech.org/ifc/IFC2x4/rc4/html/index.htm
     * https://www.cax-if.org/documents/AP242/AP242_mim_lf_1.36.htm
     */
	/* in diesen Dateien finden sich die häufigsten Vorkommen der Entities:
ACTION_PROPERTY: C:\Zeichnungen\STEP\STEP data\final assembly.stp (164)
ACTION_PROPERTY_REPRESENTATION: C:\Zeichnungen\STEP\STEP data\final assembly.stp (164)
ACTION_RESOURCE_TYPE: C:\Zeichnungen\STEP\STEP data\stage 1.stp (4)
ADVANCED_BREP_SHAPE_REPRESENTATION: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12769)
ADVANCED_FACE: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (68780)
ANNOTATION_OCCURRENCE: C:\Zeichnungen\STEP\C5559N1.stp (2)
ANNOTATION_TEXT_OCCURRENCE: C:\Zeichnungen\STEP\C5559N1.stp (2)
APPLICATION_CONTEXT: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
APPLICATION_PROTOCOL_DEFINITION: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
APPLIED_APPROVAL_ASSIGNMENT: C:\Zeichnungen\STEP\STEP data\final assembly.stp (3)
APPLIED_DATE_AND_TIME_ASSIGNMENT: C:\Zeichnungen\STEP\STEP data\final assembly.stp (2)
APPLIED_GROUP_ASSIGNMENT: C:\Zeichnungen\STEP\0816.5.001.stp (1)
APPLIED_PERSON_AND_ORGANIZATION_ASSIGNMENT: C:\Zeichnungen\STEP\C5559N1.stp (4)
APPLIED_SECURITY_CLASSIFICATION_ASSIGNMENT: C:\Zeichnungen\STEP\STEP data\final assembly.stp (1)
APPROVAL: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
APPROVAL_DATE_TIME: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
APPROVAL_PERSON_ORGANIZATION: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
APPROVAL_ROLE: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
APPROVAL_STATUS: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
AXIS1_PLACEMENT: C:\Zeichnungen\STEP\83811_p9.stp (203)
AXIS2_PLACEMENT_2D: C:\Zeichnungen\STEP\exp4.stp (979)
AXIS2_PLACEMENT_3D: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (130898)
B_SPLINE_CURVE: C:\Zeichnungen\STEP\x10000202L01.stp (4040)
B_SPLINE_CURVE_WITH_KNOTS: C:\Zeichnungen\STEP\exp1.stp (32806)
B_SPLINE_SURFACE: C:\Zeichnungen\STEP\638.stp (2006)
B_SPLINE_SURFACE_WITH_KNOTS: C:\Zeichnungen\STEP\Einsatz BS.stp (4492)
BOUNDARY_CURVE: C:\Zeichnungen\STEP\1249_MF1_ELETTRODI_INTERO.stp (202)
BOUNDED_CURVE: C:\Zeichnungen\STEP\1249_MF1_ELETTRODI_INTERO.stp (16370)
BOUNDED_SURFACE: C:\Zeichnungen\STEP\638.stp (2006)
BREP_WITH_VOIDS: C:\Zeichnungen\STEP\23134-3_15555_214.stp (10)
CALENDAR_DATE: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
CARTESIAN_POINT: C:\Zeichnungen\STEP\17013100P028.stp (1769385)
CC_DESIGN_APPROVAL: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
CC_DESIGN_DATE_AND_TIME_ASSIGNMENT: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (25540)
CC_DESIGN_PERSON_AND_ORGANIZATION_ASSIGNMENT: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (63850)
CC_DESIGN_SECURITY_CLASSIFICATION: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
CIRCLE: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (49348)
CLOSED_SHELL: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12769)
COLOUR_RGB: C:\Zeichnungen\STEP\AD10986_03-03051_op30_00_Schneidmatrize-43_19706139.stp (18128)
COMPOSITE_CURVE: C:\Zeichnungen\STEP\AD10986_03-03051_op30_00_Schneidmatrize-43_19706139.stp (8709)
COMPOSITE_CURVE_SEGMENT: C:\Zeichnungen\STEP\1249_MF1_ELETTRODI_INTERO.stp (14134)
CONICAL_SURFACE: C:\Zeichnungen\STEP\638.stp (6071)
CONSTRUCTIVE_GEOMETRY_REPRESENTATION: C:\Zeichnungen\STEP\2016_001_100_$10.stp (1)
CONSTRUCTIVE_GEOMETRY_REPRESENTATION_RELATIONSHIP: C:\Zeichnungen\STEP\2016_001_100_$10.stp (1)
CONTEXT_DEPENDENT_SHAPE_REPRESENTATION: C:\Zeichnungen\STEP\T1155 Spoil Hdlg in Z.STP (662)
CONVERSION_BASED_UNIT: C:\Zeichnungen\STEP\T1155 Spoil Hdlg in Z.STP (207)
COORDINATED_UNIVERSAL_TIME_OFFSET: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
CURVE: C:\Zeichnungen\STEP\1249_MF1_ELETTRODI_INTERO.stp (16370)
CURVE_BOUNDED_SURFACE: C:\Zeichnungen\STEP\1249_MF1_ELETTRODI_INTERO.stp (2519)
CURVE_STYLE: C:\Zeichnungen\STEP\AD10986_03-03051_op30_00_Schneidmatrize-43_19706139.stp (17626)
CYLINDRICAL_SURFACE: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (24674)
DATE_AND_TIME: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
DATE_TIME_ROLE: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (25540)
DEFINITIONAL_REPRESENTATION: C:\Zeichnungen\STEP\exp1.stp (37976)
DEGENERATE_TOROIDAL_SURFACE: C:\Zeichnungen\STEP\23265-03_15565_210.stp (16)
DERIVED_UNIT: C:\Zeichnungen\STEP\4303f1_961-02.stp (10)
DERIVED_UNIT_ELEMENT: C:\Zeichnungen\STEP\4303f1_961-02.stp (10)
DESCRIPTIVE_REPRESENTATION_ITEM: C:\Zeichnungen\STEP\Baugruppe.stp (352)
DESIGN_CONTEXT: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
DIMENSIONAL_EXPONENTS: C:\Zeichnungen\STEP\T1155 Spoil Hdlg in Z.STP (207)
DIRECTION: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (341668)
DOCUMENT: C:\Zeichnungen\STEP\30secAcrobat-2,15minPCad.stp (3)
DOCUMENT_TYPE: C:\Zeichnungen\STEP\30secAcrobat-2,15minPCad.stp (3)
DRAUGHTING_ANNOTATION_OCCURRENCE: C:\Zeichnungen\STEP\C5559N1.stp (2)
DRAUGHTING_MODEL: C:\Zeichnungen\STEP\TITAN.stp (24)
DRAUGHTING_PRE_DEFINED_COLOUR: C:\Zeichnungen\STEP\Baugruppe.stp (8)
DRAUGHTING_PRE_DEFINED_CURVE_FONT: C:\Zeichnungen\STEP\AD10986_03-03051_op30_00_Schneidmatrize-43_19706139.stp (17626)
EDGE_CURVE: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (127822)
EDGE_LOOP: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (69913)
ELLIPSE: C:\Zeichnungen\STEP\0816.5.001.stp (19774)
EXPRESSION: C:\Zeichnungen\STEP\STEP data\final assembly.stp (10)
EXPRESSION_REPRESENTATION_ITEM: C:\Zeichnungen\STEP\STEP data\final assembly.stp (10)
EXTERNAL_SOURCE: C:\Zeichnungen\STEP\C5559N1.stp (1)
EXTERNALLY_DEFINED_TEXT_FONT: C:\Zeichnungen\STEP\C5559N1.stp (1)
FACE_BOUND: C:\Zeichnungen\STEP\638.stp (13019)
FACE_OUTER_BOUND: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (68780)
FACE_SURFACE: C:\Zeichnungen\STEP\842_t3x600_asm.stp (11266)
FILL_AREA_STYLE: C:\Zeichnungen\STEP\Einsatz BS.stp (9425)
FILL_AREA_STYLE_COLOUR: C:\Zeichnungen\STEP\Einsatz BS.stp (9425)
FREEFORM_MILLING_OPERATION: C:\Zeichnungen\STEP\STEP data\final assembly.stp (9)
GENERAL_PROPERTY: C:\Zeichnungen\STEP\Baugruppe.stp (352)
GENERAL_PROPERTY_ASSOCIATION: C:\Zeichnungen\STEP\Baugruppe.stp (352)
GENERIC_EXPRESSION: C:\Zeichnungen\STEP\STEP data\final assembly.stp (10)
GENERIC_LITERAL: C:\Zeichnungen\STEP\STEP data\final assembly.stp (5)
GENERIC_VARIABLE: C:\Zeichnungen\STEP\STEP data\final assembly.stp (5)
GEOMETRIC_CURVE_SET: C:\Zeichnungen\STEP\9999_EAS-1.1.stp (35)
GEOMETRIC_REPRESENTATION_CONTEXT: C:\Zeichnungen\STEP\exp1.stp (38038)
GEOMETRIC_REPRESENTATION_ITEM: C:\Zeichnungen\STEP\1249_MF1_ELETTRODI_INTERO.stp (18129)
GEOMETRIC_SET: C:\Zeichnungen\STEP\1249_MF1_ELETTRODI_INTERO.stp (10)
GEOMETRICALLY_BOUNDED_SURFACE_SHAPE_REPRESENTATION: C:\Zeichnungen\STEP\1249_MF1_ELETTRODI_INTERO.stp (10)
GEOMETRICALLY_BOUNDED_WIREFRAME_SHAPE_REPRESENTATION: C:\Zeichnungen\STEP\AD10986_03-03051_op30_00_Schneidmatrize-43_19706139.stp (30)
GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT: C:\Zeichnungen\STEP\exp3.stp (750)
GLOBAL_UNIT_ASSIGNED_CONTEXT: C:\Zeichnungen\STEP\exp3.stp (750)
GROUP: C:\Zeichnungen\STEP\0816.5.001.stp (1)
INSTANCED_FEATURE: C:\Zeichnungen\STEP\STEP data\final assembly.stp (1)
INT_LITERAL: C:\Zeichnungen\STEP\STEP data\final assembly.stp (1)
INVISIBILITY: C:\Zeichnungen\STEP\C5559N1.stp (10)
ITEM_DEFINED_TRANSFORMATION: C:\Zeichnungen\STEP\T1155 Spoil Hdlg in Z.STP (662)
LENGTH_MEASURE_WITH_UNIT: C:\Zeichnungen\STEP\T1155 Spoil Hdlg in Z.STP (207)
LENGTH_UNIT: C:\Zeichnungen\STEP\exp3.stp (750)
LINE: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (79872)
LITERAL_NUMBER: C:\Zeichnungen\STEP\STEP data\final assembly.stp (5)
LOCAL_TIME: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
MACHINING_FEATURE_PROCESS: C:\Zeichnungen\STEP\STEP data\final assembly.stp (1)
MACHINING_FEATURE_RELATIONSHIP: C:\Zeichnungen\STEP\STEP data\final assembly.stp (13)
MACHINING_FEED_SPEED_REPRESENTATION: C:\Zeichnungen\STEP\STEP data\stage 1.stp (11)
MACHINING_FUNCTIONS: C:\Zeichnungen\STEP\STEP data\stage 1.stp (11)
MACHINING_FUNCTIONS_RELATIONSHIP: C:\Zeichnungen\STEP\STEP data\stage 1.stp (11)
MACHINING_NC_FUNCTION: C:\Zeichnungen\STEP\STEP data\final assembly.stp (1)
MACHINING_OPERATION_RELATIONSHIP: C:\Zeichnungen\STEP\STEP data\final assembly.stp (13)
MACHINING_PROCESS_EXECUTABLE: C:\Zeichnungen\STEP\STEP data\final assembly.stp (1)
MACHINING_PROCESS_SEQUENCE_RELATIONSHIP: C:\Zeichnungen\STEP\STEP data\final assembly.stp (20)
MACHINING_PROJECT: C:\Zeichnungen\STEP\STEP data\final assembly.stp (1)
MACHINING_PROJECT_WORKPIECE_RELATIONSHIP: C:\Zeichnungen\STEP\STEP data\final assembly.stp (1)
MACHINING_SPINDLE_SPEED_REPRESENTATION: C:\Zeichnungen\STEP\STEP data\stage 1.stp (11)
MACHINING_TECHNOLOGY: C:\Zeichnungen\STEP\STEP data\stage 1.stp (11)
MACHINING_TECHNOLOGY_RELATIONSHIP: C:\Zeichnungen\STEP\STEP data\final assembly.stp (28)
MACHINING_TOOL: C:\Zeichnungen\STEP\STEP data\final assembly.stp (6)
MACHINING_TOOL_BODY_REPRESENTATION: C:\Zeichnungen\STEP\STEP data\final assembly.stp (6)
MACHINING_TOOL_DIMENSION_REPRESENTATION: C:\Zeichnungen\STEP\STEP data\final assembly.stp (6)
MACHINING_TOOLPATH: C:\Zeichnungen\STEP\STEP data\final assembly.stp (20)
MACHINING_TOOLPATH_SEQUENCE_RELATIONSHIP: C:\Zeichnungen\STEP\STEP data\final assembly.stp (22)
MACHINING_TOOLPATH_SPEED_PROFILE_REPRESENTATION: C:\Zeichnungen\STEP\STEP data\final assembly.stp (1)
MACHINING_TOUCH_PROBING: C:\Zeichnungen\STEP\STEP data\final assembly.stp (4)
MACHINING_WORKINGSTEP: C:\Zeichnungen\STEP\STEP data\final assembly.stp (13)
MACHINING_WORKPLAN: C:\Zeichnungen\STEP\STEP data\final assembly.stp (6)
MANIFOLD_SOLID_BREP: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12769)
MANIFOLD_SURFACE_SHAPE_REPRESENTATION: C:\Zeichnungen\STEP\exp3.stp (750)
MAPPED_ITEM: C:\Zeichnungen\STEP\Zeichnungen\2200x600x625_Mitsubishi_AE1600SW_4polig.stp (266)
MEASURE_REPRESENTATION_ITEM: C:\Zeichnungen\STEP\STEP data\final assembly.stp (62)
MEASURE_WITH_UNIT: C:\Zeichnungen\STEP\STEP data\final assembly.stp (46)
MECHANICAL_CONTEXT: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
MECHANICAL_DESIGN_GEOMETRIC_PRESENTATION_REPRESENTATION: C:\Zeichnungen\STEP\exp3.stp (750)
NAMED_UNIT: C:\Zeichnungen\STEP\exp3.stp (2250)
NEXT_ASSEMBLY_USAGE_OCCURRENCE: C:\Zeichnungen\STEP\T1155 Spoil Hdlg in Z.STP (662)
NUMERIC_EXPRESSION: C:\Zeichnungen\STEP\STEP data\final assembly.stp (10)
NUMERIC_VARIABLE: C:\Zeichnungen\STEP\STEP data\final assembly.stp (5)
OFFSET_SURFACE: C:\Zeichnungen\STEP\Einsatz BS.stp (245)
OPEN_SHELL: C:\Zeichnungen\STEP\L00_MASCHIO_3D+EL_pcam.stp (1509)
ORGANIZATION: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
ORIENTED_CLOSED_SHELL: C:\Zeichnungen\STEP\Ele_matrice.stp (42)
ORIENTED_EDGE: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (255644)
OUTER_BOUNDARY_CURVE: C:\Zeichnungen\STEP\1249_MF1_ELETTRODI_INTERO.stp (2519)
OVER_RIDING_STYLED_ITEM: C:\Zeichnungen\STEP\3EER400007-4374E.stp (14046)
PARAMETRIC_REPRESENTATION_CONTEXT: C:\Zeichnungen\STEP\exp1.stp (37976)
PCURVE: C:\Zeichnungen\STEP\exp1.stp (37976)
PERSON: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
PERSON_AND_ORGANIZATION: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
PERSON_AND_ORGANIZATION_ROLE: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (51080)
PERSONAL_ADDRESS: C:\Zeichnungen\STEP\00351_01_60PI0072_Tassello_B10-iniezione-1.stp (1)
PLANAR_EXTENT: C:\Zeichnungen\STEP\C5559N1.stp (2)
PLANE: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (44106)
PLANE_ANGLE_MEASURE_WITH_UNIT: C:\Zeichnungen\STEP\FE834-0_20131113.stp (199)
PLANE_ANGLE_UNIT: C:\Zeichnungen\STEP\exp3.stp (750)
POINT_STYLE: C:\Zeichnungen\STEP\5077PJP125P30-12_STEP-2.14a.stp (386)
POLYLINE: C:\Zeichnungen\STEP\STEP data\final assembly.stp (33)
PRE_DEFINED_MARKER: C:\Zeichnungen\STEP\5077PJP125P30-12_STEP-2.14a.stp (386)
PRE_DEFINED_POINT_MARKER_SYMBOL: C:\Zeichnungen\STEP\2013-or-030-021-izzi-asm_PCAM.stp (27)
PRESENTATION_LAYER_ASSIGNMENT: C:\Zeichnungen\STEP\24_PREET_ALL.stp (1059)
PRESENTATION_STYLE_ASSIGNMENT: C:\Zeichnungen\STEP\AD10986_03-03051_op30_00_Schneidmatrize-43_19706139.stp (18128)
PROCESS_PRODUCT_ASSOCIATION: C:\Zeichnungen\STEP\STEP data\final assembly.stp (5)
PROCESS_PROPERTY_ASSOCIATION: C:\Zeichnungen\STEP\STEP data\final assembly.stp (1)
PRODUCT: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
PRODUCT_CATEGORY: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
PRODUCT_CATEGORY_RELATIONSHIP: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
PRODUCT_CONTEXT: C:\Zeichnungen\STEP\10163_SF51_01_091118.stp (160)
PRODUCT_DEFINITION: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
PRODUCT_DEFINITION_CONTEXT: C:\Zeichnungen\STEP\exp3.stp (750)
PRODUCT_DEFINITION_FORMATION: C:\Zeichnungen\STEP\exp3.stp (750)
PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
PRODUCT_DEFINITION_PROCESS: C:\Zeichnungen\STEP\STEP data\final assembly.stp (5)
PRODUCT_DEFINITION_SHAPE: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
PRODUCT_DEFINITION_WITH_ASSOCIATED_DOCUMENTS: C:\Zeichnungen\STEP\30secAcrobat-2,15minPCad.stp (3)
PRODUCT_RELATED_PRODUCT_CATEGORY: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
PRODUCT_TYPE: C:\Zeichnungen\STEP\exp3.stp (750)
PROPERTY_DEFINITION: C:\Zeichnungen\STEP\Baugruppe.stp (377)
PROPERTY_DEFINITION_REPRESENTATION: C:\Zeichnungen\STEP\Baugruppe.stp (375)
PROPERTY_PROCESS: C:\Zeichnungen\STEP\STEP data\final assembly.stp (1)
QUASI_UNIFORM_CURVE: C:\Zeichnungen\STEP\1249_MF1_ELETTRODI_INTERO.stp (4554)
QUASI_UNIFORM_SURFACE: C:\Zeichnungen\STEP\LHWE0059PCAM.stp (118)
RATIONAL_B_SPLINE_CURVE: C:\Zeichnungen\STEP\x10000202L01.stp (4040)
RATIONAL_B_SPLINE_SURFACE: C:\Zeichnungen\STEP\638.stp (2006)
REAL_LITERAL: C:\Zeichnungen\STEP\STEP data\final assembly.stp (4)
REAL_NUMERIC_VARIABLE: C:\Zeichnungen\STEP\STEP data\final assembly.stp (5)
REPRESENTATION: C:\Zeichnungen\STEP\Baugruppe.stp (375)
REPRESENTATION_CONTEXT: C:\Zeichnungen\STEP\exp1.stp (38038)
REPRESENTATION_ITEM: C:\Zeichnungen\STEP\1249_MF1_ELETTRODI_INTERO.stp (18129)
REPRESENTATION_ITEM_RELATIONSHIP: C:\Zeichnungen\STEP\STEP data\final assembly.stp (4)
REPRESENTATION_MAP: C:\Zeichnungen\STEP\Zeichnungen\2200x600x625_Mitsubishi_AE1600SW_4polig.stp (266)
REPRESENTATION_RELATIONSHIP: C:\Zeichnungen\STEP\T1155 Spoil Hdlg in Z.STP (662)
REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION: C:\Zeichnungen\STEP\T1155 Spoil Hdlg in Z.STP (662)
RESOURCE_PROPERTY: C:\Zeichnungen\STEP\STEP data\final assembly.stp (6)
RESOURCE_PROPERTY_REPRESENTATION: C:\Zeichnungen\STEP\STEP data\final assembly.stp (6)
SEAM_CURVE: C:\Zeichnungen\STEP\Hembra.stp (64)
SECURITY_CLASSIFICATION: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
SECURITY_CLASSIFICATION_LEVEL: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
SHAPE_ASPECT: C:\Zeichnungen\STEP\Solid_Points.stp (8)
SHAPE_DEFINITION_REPRESENTATION: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (12770)
SHAPE_REPRESENTATION: C:\Zeichnungen\STEP\T1155 Spoil Hdlg in Z.STP (207)
SHAPE_REPRESENTATION_RELATIONSHIP: C:\Zeichnungen\STEP\T1155 Spoil Hdlg in Z.STP (821)
SHAPE_REPRESENTATION_WITH_PARAMETERS: C:\Zeichnungen\STEP\STEP data\final assembly.stp (1)
SHELL_BASED_SURFACE_MODEL: C:\Zeichnungen\STEP\exp3.stp (750)
SI_UNIT: C:\Zeichnungen\STEP\exp3.stp (2250)
SIMPLE_GENERIC_EXPRESSION: C:\Zeichnungen\STEP\STEP data\final assembly.stp (10)
SIMPLE_NUMERIC_EXPRESSION: C:\Zeichnungen\STEP\STEP data\final assembly.stp (10)
SOLID_ANGLE_UNIT: C:\Zeichnungen\STEP\exp3.stp (750)
SPHERICAL_SURFACE: C:\Zeichnungen\STEP\17013100E151.stp (447)
STYLED_ITEM: C:\Zeichnungen\STEP\AD10986_03-03051_op30_00_Schneidmatrize-43_19706139.stp (17920)
SURFACE: C:\Zeichnungen\STEP\638.stp (2006)
SURFACE_CURVE: C:\Zeichnungen\STEP\exp1.stp (18988)
SURFACE_OF_LINEAR_EXTRUSION: C:\Zeichnungen\STEP\2014-OR-126-002-ele-asm.stp (1282)
SURFACE_OF_REVOLUTION: C:\Zeichnungen\STEP\83811_p9.stp (203)
SURFACE_SIDE_STYLE: C:\Zeichnungen\STEP\Einsatz BS.stp (9425)
SURFACE_STYLE_BOUNDARY: C:\Zeichnungen\STEP\3EER400007-4374E.stp (3986)
SURFACE_STYLE_FILL_AREA: C:\Zeichnungen\STEP\Einsatz BS.stp (9425)
SURFACE_STYLE_PARAMETER_LINE: C:\Zeichnungen\STEP\3EER400007-4374E.stp (3986)
SURFACE_STYLE_USAGE: C:\Zeichnungen\STEP\Einsatz BS.stp (9425)
TEXT_LITERAL_WITH_EXTENT: C:\Zeichnungen\STEP\C5559N1.stp (2)
TEXT_STYLE_FOR_DEFINED_FONT: C:\Zeichnungen\STEP\C5559N1.stp (2)
TEXT_STYLE_WITH_BOX_CHARACTERISTICS: C:\Zeichnungen\STEP\C5559N1.stp (2)
TIME_MEASURE_WITH_UNIT: C:\Zeichnungen\STEP\STEP data\final assembly.stp (1)
TIME_UNIT: C:\Zeichnungen\STEP\STEP data\final assembly.stp (2)
TOROIDAL_SURFACE: C:\Zeichnungen\STEP\exp1.stp (625)
TRIMMED_CURVE: C:\Zeichnungen\STEP\AD10986_03-03051_op30_00_Schneidmatrize-43_19706139.stp (8709)
UNCERTAINTY_MEASURE_WITH_UNIT: C:\Zeichnungen\STEP\exp3.stp (750)
VALUE_REPRESENTATION_ITEM: C:\Zeichnungen\STEP\4303f1_961-02.stp (8)
VARIABLE: C:\Zeichnungen\STEP\STEP data\final assembly.stp (5)
VECTOR: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (79872)
VERTEX_LOOP: C:\Zeichnungen\STEP\AS_montiert.stp (104)
VERTEX_POINT: C:\Zeichnungen\STEP\Ligna - Staab - Halle 1.stp (85207)
    */

	/// <summary>
	/// A collection of edges created in the course of reading a step file. Its purpose is to use the same edge for the
	/// use on the second face. And also to determin, when an edge had to be splitted into parts durng the import, to keep
	/// faces connected to the same edges.
	/// Find edge corresponding to two vertices and a 3d curve. There might be more than one edge connecting two vertices, that is
	/// why we need the curve. And the edge may have been splitted into two ore more subedges (as a result to periodic faces/surfaces)
	/// </summary>
	internal class StepEdgeCollection
	{
		class SortedVertexPair
		{
			Vertex v1;
			Vertex v2;
			public SortedVertexPair(Vertex v1, Vertex v2)
			{
				if (v1.GetHashCode() < v2.GetHashCode())
				{
					this.v1 = v1;
					this.v2 = v2;
				}
				else
				{
					this.v1 = v2;
					this.v2 = v1;
				}
			}
			public override bool Equals(object obj)
			{
				if (obj is SortedVertexPair)
				{
					return (obj as SortedVertexPair).v1 == v1 && (obj as SortedVertexPair).v2 == v2;
				}
				return false;
			}
			public override int GetHashCode()
			{
				return v1.GetHashCode() ^ v2.GetHashCode();
			}
		}
		Dictionary<SortedVertexPair, List<List<Edge>>> collection; // contains one or more edge to a pair of vertices
																   // if an edge has been splitted, the List<Edge> starts with the unsplitted Edge followed by the parts
		public StepEdgeCollection()
		{
			collection = new Dictionary<SortedVertexPair, List<List<Edge>>>();
		}
		public void Add(Edge toAdd)
		{
			SortedVertexPair vp = new SortedVertexPair(toAdd.Vertex1, toAdd.Vertex2);
			List<List<Edge>> edges;
			if (!collection.TryGetValue(vp, out edges))
			{
				edges = new List<List<Edge>>();
				collection[vp] = edges;
			}
			if (edges.Count == 0)
			{
				edges.Add(new List<Edge> { toAdd });
			}
			else
			{
				List<Edge> found = null;
				for (int i = 0; i < edges.Count; i++)
				{
					if (edges[i][0].Curve3D == null && toAdd.Curve3D == null)
					{   // a pole. it will never be splitted
						edges[i][0] = toAdd;
					}
					else if (edges[i][0].Curve3D != null)
					{
						if (toAdd.Curve3D != null)
						{
							if (edges[i][0].Curve3D.DistanceTo(toAdd.Curve3D.PointAt(0.5)) < Precision.eps)
							{
								found = edges[i];
								break;
							}
						}
					}
				}
				if (found != null) found[0] = toAdd;
				else edges.Add(new List<Edge> { toAdd });
			}
		}
		public List<Edge> GetEdge(Vertex v1, Vertex v2, ICurve curve3d)
		{
			SortedVertexPair vp = new SortedVertexPair(v1, v2);
			List<List<Edge>> edges;
			if (collection.TryGetValue(vp, out edges))
			{
				List<List<Edge>> lle = collection[vp];
				for (int i = 0; i < lle.Count; i++)
				{
					if (lle[i][0].Curve3D == null && curve3d == null) return lle[i];
					if (lle[i][0].Curve3D != null && curve3d != null)
					{
						if (lle[i][0].Curve3D.DistanceTo(curve3d.PointAt(0.5)) < Precision.eps) return lle[i];
					}
				}
			}
			return null;
		}
		public void AddSplitted(Vertex v1, Vertex v2, ICurve curve3d, List<Edge> parts)
		{
			List<Edge> found = GetEdge(v1, v2, curve3d);
			if (found == null)
			{
				Edge edg = new Edge(null, curve3d);
				edg.UseVertices(v1, v2);
				found = new List<Edge> { edg };
			}
			found.AddRange(parts);
		}
	}
	internal class context
	{
		public double toRadian;
		public double uncertainty; // from UNCERTAINTY_MEASURE_WITH_UNIT
		public double factor = 1.0;
	}
	/// <summary>
	/// CImport and conversion of STEP files into CADability objects.
	/// there are some global settings, which influence the import:
	/// <list type="bullet">
	/// <item>StepImport.Parallel: uses multi-threading during import</item>
	/// <item>StepImport.CombineFaces: adjacent faces with the same surface may be combined to a single face</item>
	/// </list>
	/// </summary>
	public class ImportStep
	{
#if DEBUG
		public SortedDictionary<string, int> allNames;
		public Dictionary<string, HashSet<string>> entityPattern; // 
		public Stack<int> definitionStack;
		static int faceCount = 0;
#endif
		long elapsedMs = 0;
		int defIndex = 0;
		private bool transformationMode;
		private bool makeBlocks = false;

		Tokenizer tk;
		List<Item> definitions;
		private int numFaces, createdFaces;

		//StepEdgeCollection edgeCollection; // collects the edges during face and shell construction to reuse already created edges
		private context context; // convert angle to radian, usually angles are provided as radian, so this factor is 1.0
		HashSet<Item> products = new HashSet<Item>();
		HashSet<Item> mappedItems = new HashSet<Item>();
		private Dictionary<int, string> importProblems;
		private List<Item> notImportedFaces;
		private Dictionary<string, ColorDef> definedColors;
		private System.Collections.Concurrent.ConcurrentDictionary<string, IGeoObject> namedGeoObjects = new System.Collections.Concurrent.ConcurrentDictionary<string, IGeoObject>();
		private System.Collections.Concurrent.ConcurrentDictionary<NurbsSurface, ModOp2D> nurbsSurfacesWithModifiedKnots; // when in construction the nurbs surface get a different 2d space, the modification is saved here
		class Tokenizer : IDisposable
		{
			StreamReader sr;
			string currentline;
			int actind;
			public Tokenizer(string filename)
			{
				sr = new StreamReader(filename);
				currentline = sr.ReadLine().Trim();
				actind = 0;
			}

			public bool EndOfFile
			{
				get
				{
					return sr.EndOfStream;
				}
			}

			public bool NextToken(out string line, out int start, out int length)
			{
				line = null;
				start = length = 0;
				while (actind >= currentline.Length)
				{
					if (sr.EndOfStream) return false;
					currentline = sr.ReadLine().Trim();
					actind = 0;
				}
				while (char.IsWhiteSpace(currentline[actind]))
				{   // skip whitespace
					++actind;
					if (actind >= currentline.Length)
					{
						if (sr.EndOfStream) return false;
						currentline = sr.ReadLine().Trim();
						actind = 0;
					}
				}
				if (currentline[actind] == '/' && actind < currentline.Length - 1 && currentline[actind + 1] == '*')
				{   // skip comments
					int ind = currentline.IndexOf("*/", actind + 2);
					while (ind < 0)
					{
						if (sr.EndOfStream) return false;
						currentline = sr.ReadLine().Trim();
						ind = currentline.IndexOf("*/");
					}
					actind = ind + 2;
					if (actind >= currentline.Length)
					{
						if (sr.EndOfStream) return false;
						currentline = sr.ReadLine().Trim();
						actind = 0;
					}
					return NextToken(out line, out start, out length);
				}
				if (currentline[actind] == '\'')
				{   // return string in quotes
					int ind = currentline.IndexOf('\'', actind + 1);
					while (ind >= 0 && ind < currentline.Length - 3 && currentline[ind + 1] == '\'') ind = currentline.IndexOf('\'', ind + 2); // skip double '' in a string
					if (ind < 0)
					{   // string spans multiple lines
						string res = currentline.Substring(actind);
						while (ind < 0)
						{
							if (sr.EndOfStream) return false;
							currentline = sr.ReadLine().Trim();
							ind = currentline.IndexOf('\'');
							while (ind >= 0 && ind < currentline.Length - 3 && currentline[ind + 1] == '\'') ind = currentline.IndexOf('\'', ind + 2); // skip double '' in a string
							if (ind >= 0)
							{
								res += currentline.Substring(0, ind + 1);
								actind = ind + 1;
								line = res;
								start = 0;
								length = res.Length;
								return true;
							}
							else res += currentline;
						}
					}
					start = actind;
					length = ind - start + 1;
					line = currentline;
					actind = ind + 1;
					return true;
				}
				if (char.IsLetter(currentline[actind]) || currentline[actind] == '_')
				{   // return a name (string without quotes)
					start = actind;
					++actind;
					while (actind < currentline.Length &&
						(char.IsLetterOrDigit(currentline[actind]) || currentline[actind] == '_' || currentline[actind] == '-')) ++actind;
					length = actind - start;
					line = currentline;
					return true;
				}
				if (currentline[actind] == '.' && actind < currentline.Length - 1)
				{   // return a keyword like .T.
					if (char.IsLetter(currentline[actind + 1]))
					{
						int ind = currentline.IndexOf('.', actind + 1);
						if (ind < 0) throw new ApplicationException("syntax error in step file, '.' expected: " + currentline);
						start = actind;
						length = ind - start + 1;
						line = currentline;
						actind = ind + 1;
						return true;
					}
				}
				if (currentline[actind] == '.' || currentline[actind] == '-' || currentline[actind] == '+' || char.IsDigit(currentline[actind]))
				{   // return a float
					start = actind;
					++actind;
					while (actind < currentline.Length && (char.IsDigit(currentline[actind]) || currentline[actind] == 'E' || currentline[actind] == 'e'
						|| currentline[actind] == '+' || currentline[actind] == '-' || currentline[actind] == '.')) ++actind;
					length = actind - start;
					line = currentline;
					return true;
				}
				if (currentline[actind] == '#')
				{
					start = actind;
					++actind;
					while (actind < currentline.Length && char.IsDigit(currentline[actind])) ++actind;
					length = actind - start;
					line = currentline;
					return true;
				}
				if (currentline[actind] == '=' || currentline[actind] == '(' || currentline[actind] == ')' || currentline[actind] == ','
					|| currentline[actind] == ';' || currentline[actind] == '$' || currentline[actind] == '*')
				{
					start = actind;
					++actind;
					length = 1;
					line = currentline;
					return true;
				}
				return false;
			}

			#region IDisposable Support
			private bool disposedValue = false; // To detect redundant calls

			protected virtual void Dispose(bool disposing)
			{
				if (!disposedValue)
				{
					if (disposing)
					{
						if (sr != null) sr.Dispose();
					}
					disposedValue = true;
				}
			}

			// This code added to correctly implement the disposable pattern.
			void IDisposable.Dispose()
			{
				// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
				Dispose(true);
			}
			#endregion
		}
		enum BSplineCurveForm
		{
			Polyline_Form,
			Circular_Arc,
			Elliptic_Arc,
			Parabolic_Arc,
			Hyperbolic_Arc,
			Unspecified
		};
		enum BSplineSurfaceForm
		{
			Plane_Surf,
			Cylindrical_Surf,
			Conical_Surf,
			Spherical_Surf,
			Toroidal_Surf,
			Surf_Of_Revolution,
			Ruled_Surf,
			Generalised_Cone,
			Quadric_Surf,
			Surf_Of_Linear_Extrusion,
			Unspecified
		};
		enum Knot_Type
		{
			Uniform_Knots,
			Quasi_Uniform_Knots,
			Piecewise_Bezier_Knots,
			Unspecified
		};
#if DEBUG
		/// <summary>
		/// A class to visualize step items in the quick watch
		/// </summary>
		class DebugItem
		{
			public string type;
			public object parameter;
			public DebugItem(Item item)
			{
				this.type = item.type.ToString() + " (" + item.definingIndex.ToString() + ")";
				if (item.parameter != null)
				{
					Dictionary<string, DebugItem> dict = new Dictionary<string, DebugItem>();
					this.parameter = dict;
					foreach (KeyValuePair<string, Item> kv in item.parameter)
					{
						dict.Add(kv.Key, new DebugItem(kv.Value));
					}
				}
				else if (item.type == Item.ItemType.list)
				{
					List<DebugItem> list = new List<DebugItem>(item.lval.Count);
					parameter = list;
					for (int i = 0; i < item.lval.Count; i++)
					{
						list.Add(new DebugItem(item.lval[i]));
					}
				}
				else if (item.type == Item.ItemType.stringval)
				{
					this.type = item.type.ToString() + " (" + item.sval + ")";
				}
			}
		}
#endif
		[DebuggerDisplayAttribute("Item: {DebugString}")]
		class Item
		{
			public enum ItemType
			{
				invalid, intval, floatval, stringval, index, name, keyword, list, composed, created, not_set, use_default, stepEntitiesFollow,
				product, applicationContext, productDefinition, securityClassification, securityClassificationLevel, cartesianPoint, direction, axis2Placement_3d, productDefinitionShape,
				approvalPersonOrganization, personAndOrganization, person, organization, approval, approvalStatus, approvalRole, dateAndTime, localTime,
				coordinatedUniversalTimeOffset, orientedEdge, faceBound, closedShell, vector, advancedBrepShapeRepresentation, shapeRepresentation, advancedFace,
				applicationProtocolDefinition, approvalDateTime, axis1Placement, bSplineCurveWithKnots, bSplineSurfaceWithKnots, manifoldSolidBrep, calendarDate,
				ccDesignApproval, ccDesignDateAndTimeAssignment, dateTimeRole, ccDesignPersonAndOrganizationAssignment, personAndOrganizationRole, ccDesignSecurityClassification,
				circle, conicalSurface, shapeRepresentationRelationship, cylindricalSurface, toroidalSurface, designContext, edgeCurve, edgeLoop, faceOuterBound, geometricSet,
				geometricallyBoundedSurfaceShapeRepresentation, uncertaintyMeasureWithUnit, line, mechanicalContext, personalAddress, plane, planeAngleMeasureWithUnit, planeAngleMeasure,
				productCategory, productCategoryRelationship, productDefinitionFormationWithSpecifiedSource, productRelatedProductCategory, shapeDefinitionRepresentation,
				sphericalSurface, surfaceOfRevolution, vertexPoint, lengthUnit, namedUnit, siUnit, planeAngleUnit, solidAngleUnit, geometricRepresentationContext,
				globalUncertaintyAssignedContext, globalUnitAssignedContext, representationContext, mechanicalDesignGeometricPresentationRepresentation, productDefinitionContext,
				styledItem, conversionBasedUnit, presentationStyleAssignment, lengthMeasureWithUnit, dimensionalExponents, surfaceStyleUsage, productContext, surfaceSideStyle,
				surfaceStyleFillArea, fillAreaStyle, fillAreaStyleColour, colourRgb, presentationLayerAssignment, productDefinitionFormation, surfaceCurve, pcurve,
				definitionalRepresentation, parametricRepresentationContext, boundedCurve, bSplineCurve, curve, geometricRepresentationItem, rationalBSplineCurve,
				representationItem, axis2Placement_2d, curveStyle, draughtingPreDefinedCurveFont, surfaceOfLinearExtrusion, ellipse, manifoldSurfaceShapeRepresentation,
				faceSurface, mappedItem, brepWithVoids, shellBasedSurfaceModel, boundedSurface, bSplineSurface, rationalBSplineSurface, surface, offsetSurface,
				representationMap, orientedClosedShell, vertexLoop, draughtingPreDefinedColour, quasiUniformCurve, nextAssemblyUsageOccurrence, itemDefinedTransformation,
				representationRelationship, representationRelationshipWithTransformation, contextDependentShapeRepresentation, group, appliedGroupAssignment, openShell,
				seamCurve, productType, overRidingStyledItem, geometricCurveSet, geometricallyBoundedWireframeShapeRepresentation, pointStyle, trimmedCurve, compositeCurve,
				compositeCurveSegment, preDefinedMarker, outerBoundaryCurve, curveBoundedSurface, boundaryCurve, quasiUniformSurface, propertyDefinitionRepresentation,
				representation, propertyDefinition, valueRepresentationItem, preDefinedPointMarkerSymbol, constructiveGeometryRepresentation, constructiveGeometryRepresentationRelationship,
				degenerateToroidalSurface, draughtingModel, document, documentType, productDefinitionWithAssociatedDocuments, measureWithUnit, surfaceStyleBoundary,
				surfaceStyleParameterLine, derivedUnitElement, derivedUnit, measureRepresentationItem, generalProperty, generalPropertyAssociation, descriptiveRepresentationItem,
				invisibility, shapeAspect, appliedPersonAndOrganizationAssignment, polyline, textLiteralWithExtent, planarExtent, externalSource, externallyDefinedTextFont,
				textStyleWithBoxCharacteristics, textStyleForDefinedFont, annotationOccurrence, annotationTextOccurrence, draughtingAnnotationOccurrence, machiningProject,
				processProductAssociation, productDefinitionProcess, machiningProjectWorkpieceRelationship, machiningWorkplan, machiningProcessSequenceRelationship,
				appliedDateAndTimeAssignment, appliedApprovalAssignment, machiningWorkingstep, actionProperty, actionPropertyRepresentation, machiningOperationRelationship,
				machiningFeatureRelationship, machiningTool, actionResourceType, resourceProperty, resourcePropertyRepresentation, machiningToolBodyRepresentation,
				machiningToolDimensionRepresentation, machiningTechnology, machiningSpindleSpeedRepresentation, timeUnit, timeMeasureWithUnit, machiningFeedSpeedRepresentation,
				machiningFunctions, freeformMillingOperation, machiningFunctionsRelationship, machiningTechnologyRelationship, machiningToolpathSequenceRelationship,
				instancedFeature, shapeRepresentationWithParameters, machiningFeatureProcess, propertyProcess, processPropertyAssociation, machiningToolpath,
				machiningTouchProbing, expression, expressionRepresentationItem, genericExpression, genericVariable, numericExpression, numericVariable, realNumericVariable,
				simpleGenericExpression, simpleNumericExpression, variable, representationItemRelationship, genericLiteral, literalNumber, realLiteral, machiningNcFunction,
				machiningProcessExecutable, intLiteral, machiningToolpathSpeedProfileRepresentation, appliedSecurityClassificationAssignment, markerType, positiveLengthMeasure, parameterValue
			};
			public ItemType type;
			public object val;
			public Dictionary<string, Item> parameter;
			public int definingIndex;
#if DEBUG
			public List<Item> usedBy;
			public void SetUsedBy(Item item)
			{
				usedBy.Add(item);
				if (type == ItemType.list && definingIndex <= 0)
				{
					for (int i = 0; i < (val as List<Item>).Count; i++)
					{
						(val as List<Item>)[i].SetUsedBy(item);
					}
				}
			}
			internal string DebugString
			{
				get
				{
					return type.ToString();
				}
			}
#endif
#if DEBUG
			static int counter = 0;
#endif
			public Item(ItemType type, object val)
			{
				this.type = type;
				this.val = val;
				parameter = new Dictionary<string, Item>();
#if DEBUG
				this.definingIndex = --counter;
				if (definingIndex == -1051604) { }
				usedBy = new List<Item>();
#endif
			}
			public Item Clone()
			{
				return new Item(type, val);
			}
			public static Dictionary<string, ItemType> TypeOfName = new Dictionary<string, ItemType>();

			public bool IsEntity
			{
				get
				{
					return type >= ItemType.stepEntitiesFollow;
				}
			}

			public static void Init()
			{
				TypeOfName["PRODUCT"] = ItemType.product;
				TypeOfName["APPLICATION_CONTEXT"] = ItemType.applicationContext;
				TypeOfName["PRODUCT_DEFINITION"] = ItemType.productDefinition;
				TypeOfName["SECURITY_CLASSIFICATION"] = ItemType.securityClassification;
				TypeOfName["SECURITY_CLASSIFICATION_LEVEL"] = ItemType.securityClassificationLevel;
				TypeOfName["CARTESIAN_POINT"] = ItemType.cartesianPoint;
				TypeOfName["DIRECTION"] = ItemType.direction;
				TypeOfName["AXIS2_PLACEMENT_3D"] = ItemType.axis2Placement_3d;
				TypeOfName["PRODUCT_DEFINITION_SHAPE"] = ItemType.productDefinitionShape;
				TypeOfName["APPROVAL_PERSON_ORGANIZATION"] = ItemType.approvalPersonOrganization;
				TypeOfName["PERSON_AND_ORGANIZATION"] = ItemType.personAndOrganization;
				TypeOfName["PERSON"] = ItemType.person;
				TypeOfName["ORGANIZATION"] = ItemType.organization;
				TypeOfName["APPROVAL"] = ItemType.approval;
				TypeOfName["APPROVAL_STATUS"] = ItemType.approvalStatus;
				TypeOfName["APPROVAL_ROLE"] = ItemType.approvalRole;
				TypeOfName["DATE_AND_TIME"] = ItemType.dateAndTime;
				TypeOfName["LOCAL_TIME"] = ItemType.localTime;
				TypeOfName["COORDINATED_UNIVERSAL_TIME_OFFSET"] = ItemType.coordinatedUniversalTimeOffset;
				TypeOfName["ORIENTED_EDGE"] = ItemType.orientedEdge;
				TypeOfName["FACE_BOUND"] = ItemType.faceBound;
				TypeOfName["CLOSED_SHELL"] = ItemType.closedShell;
				TypeOfName["VECTOR"] = ItemType.vector;
				TypeOfName["ADVANCED_BREP_SHAPE_REPRESENTATION"] = ItemType.advancedBrepShapeRepresentation;
				TypeOfName["SHAPE_REPRESENTATION"] = ItemType.shapeRepresentation;
				TypeOfName["ADVANCED_FACE"] = ItemType.advancedFace;
				TypeOfName["APPLICATION_PROTOCOL_DEFINITION"] = ItemType.applicationProtocolDefinition;
				TypeOfName["APPROVAL_DATE_TIME"] = ItemType.approvalDateTime;
				TypeOfName["AXIS1_PLACEMENT"] = ItemType.axis1Placement;
				TypeOfName["B_SPLINE_CURVE_WITH_KNOTS"] = ItemType.bSplineCurveWithKnots;
				TypeOfName["B_SPLINE_SURFACE_WITH_KNOTS"] = ItemType.bSplineSurfaceWithKnots;
				TypeOfName["MANIFOLD_SOLID_BREP"] = ItemType.manifoldSolidBrep;
				TypeOfName["CALENDAR_DATE"] = ItemType.calendarDate;
				TypeOfName["CC_DESIGN_APPROVAL"] = ItemType.ccDesignApproval;
				TypeOfName["CC_DESIGN_DATE_AND_TIME_ASSIGNMENT"] = ItemType.ccDesignDateAndTimeAssignment;
				TypeOfName["DATE_TIME_ROLE"] = ItemType.dateTimeRole;
				TypeOfName["CC_DESIGN_PERSON_AND_ORGANIZATION_ASSIGNMENT"] = ItemType.ccDesignPersonAndOrganizationAssignment;
				TypeOfName["PERSON_AND_ORGANIZATION_ROLE"] = ItemType.personAndOrganizationRole;
				TypeOfName["CC_DESIGN_SECURITY_CLASSIFICATION"] = ItemType.ccDesignSecurityClassification;
				TypeOfName["CIRCLE"] = ItemType.circle;
				TypeOfName["CONICAL_SURFACE"] = ItemType.conicalSurface;
				TypeOfName["SHAPE_REPRESENTATION_RELATIONSHIP"] = ItemType.shapeRepresentationRelationship;
				TypeOfName["CYLINDRICAL_SURFACE"] = ItemType.cylindricalSurface;
				TypeOfName["TOROIDAL_SURFACE"] = ItemType.toroidalSurface;
				TypeOfName["DESIGN_CONTEXT"] = ItemType.designContext;
				TypeOfName["EDGE_CURVE"] = ItemType.edgeCurve;
				TypeOfName["EDGE_LOOP"] = ItemType.edgeLoop;
				TypeOfName["FACE_OUTER_BOUND"] = ItemType.faceOuterBound;
				TypeOfName["GEOMETRIC_SET"] = ItemType.geometricSet;
				TypeOfName["GEOMETRICALLY_BOUNDED_SURFACE_SHAPE_REPRESENTATION"] = ItemType.geometricallyBoundedSurfaceShapeRepresentation;
				TypeOfName["UNCERTAINTY_MEASURE_WITH_UNIT"] = ItemType.uncertaintyMeasureWithUnit;
				TypeOfName["LINE"] = ItemType.line;
				TypeOfName["MECHANICAL_CONTEXT"] = ItemType.mechanicalContext;
				TypeOfName["PERSONAL_ADDRESS"] = ItemType.personalAddress;
				TypeOfName["PLANE"] = ItemType.plane;
				TypeOfName["PLANE_ANGLE_MEASURE_WITH_UNIT"] = ItemType.planeAngleMeasureWithUnit;
				TypeOfName["PLANE_ANGLE_MEASURE"] = ItemType.planeAngleMeasure;
				TypeOfName["PRODUCT_CATEGORY"] = ItemType.productCategory;
				TypeOfName["PRODUCT_CATEGORY_RELATIONSHIP"] = ItemType.productCategoryRelationship;
				TypeOfName["PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE"] = ItemType.productDefinitionFormationWithSpecifiedSource;
				TypeOfName["PRODUCT_RELATED_PRODUCT_CATEGORY"] = ItemType.productRelatedProductCategory;
				TypeOfName["SHAPE_DEFINITION_REPRESENTATION"] = ItemType.shapeDefinitionRepresentation;
				TypeOfName["SPHERICAL_SURFACE"] = ItemType.sphericalSurface;
				TypeOfName["SURFACE_OF_REVOLUTION"] = ItemType.surfaceOfRevolution;
				TypeOfName["VERTEX_POINT"] = ItemType.vertexPoint;
				TypeOfName["LENGTH_UNIT"] = ItemType.lengthUnit;
				TypeOfName["NAMED_UNIT"] = ItemType.namedUnit;
				TypeOfName["SI_UNIT"] = ItemType.siUnit;
				TypeOfName["PLANE_ANGLE_UNIT"] = ItemType.planeAngleUnit;
				TypeOfName["SOLID_ANGLE_UNIT"] = ItemType.solidAngleUnit;
				TypeOfName["GEOMETRIC_REPRESENTATION_CONTEXT"] = ItemType.geometricRepresentationContext;
				TypeOfName["GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT"] = ItemType.globalUncertaintyAssignedContext;
				TypeOfName["GLOBAL_UNIT_ASSIGNED_CONTEXT"] = ItemType.globalUnitAssignedContext;
				TypeOfName["REPRESENTATION_CONTEXT"] = ItemType.representationContext;
				TypeOfName["MECHANICAL_DESIGN_GEOMETRIC_PRESENTATION_REPRESENTATION"] = ItemType.mechanicalDesignGeometricPresentationRepresentation;
				TypeOfName["PRODUCT_DEFINITION_CONTEXT"] = ItemType.productDefinitionContext;
				TypeOfName["STYLED_ITEM"] = ItemType.styledItem;
				TypeOfName["CONVERSION_BASED_UNIT"] = ItemType.conversionBasedUnit;
				TypeOfName["PRESENTATION_STYLE_ASSIGNMENT"] = ItemType.presentationStyleAssignment;
				TypeOfName["LENGTH_MEASURE_WITH_UNIT"] = ItemType.lengthMeasureWithUnit;
				TypeOfName["DIMENSIONAL_EXPONENTS"] = ItemType.dimensionalExponents;
				TypeOfName["SURFACE_STYLE_USAGE"] = ItemType.surfaceStyleUsage;
				TypeOfName["PRODUCT_CONTEXT"] = ItemType.productContext;
				TypeOfName["SURFACE_SIDE_STYLE"] = ItemType.surfaceSideStyle;
				TypeOfName["SURFACE_STYLE_FILL_AREA"] = ItemType.surfaceStyleFillArea;
				TypeOfName["FILL_AREA_STYLE"] = ItemType.fillAreaStyle;
				TypeOfName["FILL_AREA_STYLE_COLOUR"] = ItemType.fillAreaStyleColour;
				TypeOfName["COLOUR_RGB"] = ItemType.colourRgb;
				TypeOfName["PRESENTATION_LAYER_ASSIGNMENT"] = ItemType.presentationLayerAssignment;
				TypeOfName["PRODUCT_DEFINITION_FORMATION"] = ItemType.productDefinitionFormation;
				TypeOfName["SURFACE_CURVE"] = ItemType.surfaceCurve;
				TypeOfName["PCURVE"] = ItemType.pcurve;
				TypeOfName["DEFINITIONAL_REPRESENTATION"] = ItemType.definitionalRepresentation;
				TypeOfName["PARAMETRIC_REPRESENTATION_CONTEXT"] = ItemType.parametricRepresentationContext;
				TypeOfName["BOUNDED_CURVE"] = ItemType.boundedCurve;
				TypeOfName["B_SPLINE_CURVE"] = ItemType.bSplineCurve;
				TypeOfName["CURVE"] = ItemType.curve;
				TypeOfName["GEOMETRIC_REPRESENTATION_ITEM"] = ItemType.geometricRepresentationItem;
				TypeOfName["RATIONAL_B_SPLINE_CURVE"] = ItemType.rationalBSplineCurve;
				TypeOfName["REPRESENTATION_ITEM"] = ItemType.representationItem;
				TypeOfName["AXIS2_PLACEMENT_2D"] = ItemType.axis2Placement_2d;
				TypeOfName["CURVE_STYLE"] = ItemType.curveStyle;
				TypeOfName["DRAUGHTING_PRE_DEFINED_CURVE_FONT"] = ItemType.draughtingPreDefinedCurveFont;
				TypeOfName["SURFACE_OF_LINEAR_EXTRUSION"] = ItemType.surfaceOfLinearExtrusion;
				TypeOfName["ELLIPSE"] = ItemType.ellipse;
				TypeOfName["MANIFOLD_SURFACE_SHAPE_REPRESENTATION"] = ItemType.manifoldSurfaceShapeRepresentation;
				TypeOfName["FACE_SURFACE"] = ItemType.faceSurface;
				TypeOfName["MAPPED_ITEM"] = ItemType.mappedItem;
				TypeOfName["BREP_WITH_VOIDS"] = ItemType.brepWithVoids;
				TypeOfName["SHELL_BASED_SURFACE_MODEL"] = ItemType.shellBasedSurfaceModel;
				TypeOfName["BOUNDED_SURFACE"] = ItemType.boundedSurface;
				TypeOfName["B_SPLINE_SURFACE"] = ItemType.bSplineSurface;
				TypeOfName["RATIONAL_B_SPLINE_SURFACE"] = ItemType.rationalBSplineSurface;
				TypeOfName["SURFACE"] = ItemType.surface;
				TypeOfName["OFFSET_SURFACE"] = ItemType.offsetSurface;
				TypeOfName["REPRESENTATION_MAP"] = ItemType.representationMap;
				TypeOfName["ORIENTED_CLOSED_SHELL"] = ItemType.orientedClosedShell;
				TypeOfName["VERTEX_LOOP"] = ItemType.vertexLoop;
				TypeOfName["DRAUGHTING_PRE_DEFINED_COLOUR"] = ItemType.draughtingPreDefinedColour;
				TypeOfName["QUASI_UNIFORM_CURVE"] = ItemType.quasiUniformCurve;
				TypeOfName["NEXT_ASSEMBLY_USAGE_OCCURRENCE"] = ItemType.nextAssemblyUsageOccurrence;
				TypeOfName["ITEM_DEFINED_TRANSFORMATION"] = ItemType.itemDefinedTransformation;
				TypeOfName["REPRESENTATION_RELATIONSHIP"] = ItemType.representationRelationship;
				TypeOfName["REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION"] = ItemType.representationRelationshipWithTransformation;
				TypeOfName["CONTEXT_DEPENDENT_SHAPE_REPRESENTATION"] = ItemType.contextDependentShapeRepresentation;
				TypeOfName["GROUP"] = ItemType.group;
				TypeOfName["APPLIED_GROUP_ASSIGNMENT"] = ItemType.appliedGroupAssignment;
				TypeOfName["OPEN_SHELL"] = ItemType.openShell;
				TypeOfName["SEAM_CURVE"] = ItemType.seamCurve;
				TypeOfName["PRODUCT_TYPE"] = ItemType.productType;
				TypeOfName["OVER_RIDING_STYLED_ITEM"] = ItemType.overRidingStyledItem;
				TypeOfName["GEOMETRIC_CURVE_SET"] = ItemType.geometricCurveSet;
				TypeOfName["GEOMETRICALLY_BOUNDED_WIREFRAME_SHAPE_REPRESENTATION"] = ItemType.geometricallyBoundedWireframeShapeRepresentation;
				TypeOfName["POINT_STYLE"] = ItemType.pointStyle;
				TypeOfName["TRIMMED_CURVE"] = ItemType.trimmedCurve;
				TypeOfName["COMPOSITE_CURVE"] = ItemType.compositeCurve;
				TypeOfName["COMPOSITE_CURVE_SEGMENT"] = ItemType.compositeCurveSegment;
				TypeOfName["PRE_DEFINED_MARKER"] = ItemType.preDefinedMarker;
				TypeOfName["OUTER_BOUNDARY_CURVE"] = ItemType.outerBoundaryCurve;
				TypeOfName["CURVE_BOUNDED_SURFACE"] = ItemType.curveBoundedSurface;
				TypeOfName["BOUNDARY_CURVE"] = ItemType.boundaryCurve;
				TypeOfName["QUASI_UNIFORM_SURFACE"] = ItemType.quasiUniformSurface;
				TypeOfName["PROPERTY_DEFINITION_REPRESENTATION"] = ItemType.propertyDefinitionRepresentation;
				TypeOfName["REPRESENTATION"] = ItemType.representation;
				TypeOfName["PROPERTY_DEFINITION"] = ItemType.propertyDefinition;
				TypeOfName["VALUE_REPRESENTATION_ITEM"] = ItemType.valueRepresentationItem;
				TypeOfName["PRE_DEFINED_POINT_MARKER_SYMBOL"] = ItemType.preDefinedPointMarkerSymbol;
				TypeOfName["CONSTRUCTIVE_GEOMETRY_REPRESENTATION"] = ItemType.constructiveGeometryRepresentation;
				TypeOfName["CONSTRUCTIVE_GEOMETRY_REPRESENTATION_RELATIONSHIP"] = ItemType.constructiveGeometryRepresentationRelationship;
				TypeOfName["DEGENERATE_TOROIDAL_SURFACE"] = ItemType.degenerateToroidalSurface;
				TypeOfName["DRAUGHTING_MODEL"] = ItemType.draughtingModel;
				TypeOfName["DOCUMENT"] = ItemType.document;
				TypeOfName["DOCUMENT_TYPE"] = ItemType.documentType;
				TypeOfName["PRODUCT_DEFINITION_WITH_ASSOCIATED_DOCUMENTS"] = ItemType.productDefinitionWithAssociatedDocuments;
				TypeOfName["MEASURE_WITH_UNIT"] = ItemType.measureWithUnit;
				TypeOfName["SURFACE_STYLE_BOUNDARY"] = ItemType.surfaceStyleBoundary;
				TypeOfName["SURFACE_STYLE_PARAMETER_LINE"] = ItemType.surfaceStyleParameterLine;
				TypeOfName["DERIVED_UNIT_ELEMENT"] = ItemType.derivedUnitElement;
				TypeOfName["DERIVED_UNIT"] = ItemType.derivedUnit;
				TypeOfName["MEASURE_REPRESENTATION_ITEM"] = ItemType.measureRepresentationItem;
				TypeOfName["GENERAL_PROPERTY"] = ItemType.generalProperty;
				TypeOfName["GENERAL_PROPERTY_ASSOCIATION"] = ItemType.generalPropertyAssociation;
				TypeOfName["DESCRIPTIVE_REPRESENTATION_ITEM"] = ItemType.descriptiveRepresentationItem;
				TypeOfName["INVISIBILITY"] = ItemType.invisibility;
				TypeOfName["SHAPE_ASPECT"] = ItemType.shapeAspect;
				TypeOfName["APPLIED_PERSON_AND_ORGANIZATION_ASSIGNMENT"] = ItemType.appliedPersonAndOrganizationAssignment;
				TypeOfName["POLYLINE"] = ItemType.polyline;
				TypeOfName["TEXT_LITERAL_WITH_EXTENT"] = ItemType.textLiteralWithExtent;
				TypeOfName["PLANAR_EXTENT"] = ItemType.planarExtent;
				TypeOfName["EXTERNAL_SOURCE"] = ItemType.externalSource;
				TypeOfName["EXTERNALLY_DEFINED_TEXT_FONT"] = ItemType.externallyDefinedTextFont;
				TypeOfName["TEXT_STYLE_WITH_BOX_CHARACTERISTICS"] = ItemType.textStyleWithBoxCharacteristics;
				TypeOfName["TEXT_STYLE_FOR_DEFINED_FONT"] = ItemType.textStyleForDefinedFont;
				TypeOfName["ANNOTATION_OCCURRENCE"] = ItemType.annotationOccurrence;
				TypeOfName["ANNOTATION_TEXT_OCCURRENCE"] = ItemType.annotationTextOccurrence;
				TypeOfName["DRAUGHTING_ANNOTATION_OCCURRENCE"] = ItemType.draughtingAnnotationOccurrence;
				TypeOfName["MACHINING_PROJECT"] = ItemType.machiningProject;
				TypeOfName["PROCESS_PRODUCT_ASSOCIATION"] = ItemType.processProductAssociation;
				TypeOfName["PRODUCT_DEFINITION_PROCESS"] = ItemType.productDefinitionProcess;
				TypeOfName["MACHINING_PROJECT_WORKPIECE_RELATIONSHIP"] = ItemType.machiningProjectWorkpieceRelationship;
				TypeOfName["MACHINING_WORKPLAN"] = ItemType.machiningWorkplan;
				TypeOfName["MACHINING_PROCESS_SEQUENCE_RELATIONSHIP"] = ItemType.machiningProcessSequenceRelationship;
				TypeOfName["APPLIED_DATE_AND_TIME_ASSIGNMENT"] = ItemType.appliedDateAndTimeAssignment;
				TypeOfName["APPLIED_APPROVAL_ASSIGNMENT"] = ItemType.appliedApprovalAssignment;
				TypeOfName["MACHINING_WORKINGSTEP"] = ItemType.machiningWorkingstep;
				TypeOfName["ACTION_PROPERTY"] = ItemType.actionProperty;
				TypeOfName["ACTION_PROPERTY_REPRESENTATION"] = ItemType.actionPropertyRepresentation;
				TypeOfName["MACHINING_OPERATION_RELATIONSHIP"] = ItemType.machiningOperationRelationship;
				TypeOfName["MACHINING_FEATURE_RELATIONSHIP"] = ItemType.machiningFeatureRelationship;
				TypeOfName["MACHINING_TOOL"] = ItemType.machiningTool;
				TypeOfName["ACTION_RESOURCE_TYPE"] = ItemType.actionResourceType;
				TypeOfName["RESOURCE_PROPERTY"] = ItemType.resourceProperty;
				TypeOfName["RESOURCE_PROPERTY_REPRESENTATION"] = ItemType.resourcePropertyRepresentation;
				TypeOfName["MACHINING_TOOL_BODY_REPRESENTATION"] = ItemType.machiningToolBodyRepresentation;
				TypeOfName["MACHINING_TOOL_DIMENSION_REPRESENTATION"] = ItemType.machiningToolDimensionRepresentation;
				TypeOfName["MACHINING_TECHNOLOGY"] = ItemType.machiningTechnology;
				TypeOfName["MACHINING_SPINDLE_SPEED_REPRESENTATION"] = ItemType.machiningSpindleSpeedRepresentation;
				TypeOfName["TIME_UNIT"] = ItemType.timeUnit;
				TypeOfName["TIME_MEASURE_WITH_UNIT"] = ItemType.timeMeasureWithUnit;
				TypeOfName["MACHINING_FEED_SPEED_REPRESENTATION"] = ItemType.machiningFeedSpeedRepresentation;
				TypeOfName["MACHINING_FUNCTIONS"] = ItemType.machiningFunctions;
				TypeOfName["FREEFORM_MILLING_OPERATION"] = ItemType.freeformMillingOperation;
				TypeOfName["MACHINING_FUNCTIONS_RELATIONSHIP"] = ItemType.machiningFunctionsRelationship;
				TypeOfName["MACHINING_TECHNOLOGY_RELATIONSHIP"] = ItemType.machiningTechnologyRelationship;
				TypeOfName["MACHINING_TOOLPATH_SEQUENCE_RELATIONSHIP"] = ItemType.machiningToolpathSequenceRelationship;
				TypeOfName["INSTANCED_FEATURE"] = ItemType.instancedFeature;
				TypeOfName["SHAPE_REPRESENTATION_WITH_PARAMETERS"] = ItemType.shapeRepresentationWithParameters;
				TypeOfName["MACHINING_FEATURE_PROCESS"] = ItemType.machiningFeatureProcess;
				TypeOfName["PROPERTY_PROCESS"] = ItemType.propertyProcess;
				TypeOfName["PROCESS_PROPERTY_ASSOCIATION"] = ItemType.processPropertyAssociation;
				TypeOfName["MACHINING_TOOLPATH"] = ItemType.machiningToolpath;
				TypeOfName["MACHINING_TOUCH_PROBING"] = ItemType.machiningTouchProbing;
				TypeOfName["EXPRESSION"] = ItemType.expression;
				TypeOfName["EXPRESSION_REPRESENTATION_ITEM"] = ItemType.expressionRepresentationItem;
				TypeOfName["GENERIC_EXPRESSION"] = ItemType.genericExpression;
				TypeOfName["GENERIC_VARIABLE"] = ItemType.genericVariable;
				TypeOfName["NUMERIC_EXPRESSION"] = ItemType.numericExpression;
				TypeOfName["NUMERIC_VARIABLE"] = ItemType.numericVariable;
				TypeOfName["REAL_NUMERIC_VARIABLE"] = ItemType.realNumericVariable;
				TypeOfName["SIMPLE_GENERIC_EXPRESSION"] = ItemType.simpleGenericExpression;
				TypeOfName["SIMPLE_NUMERIC_EXPRESSION"] = ItemType.simpleNumericExpression;
				TypeOfName["VARIABLE"] = ItemType.variable;
				TypeOfName["REPRESENTATION_ITEM_RELATIONSHIP"] = ItemType.representationItemRelationship;
				TypeOfName["GENERIC_LITERAL"] = ItemType.genericLiteral;
				TypeOfName["LITERAL_NUMBER"] = ItemType.literalNumber;
				TypeOfName["REAL_LITERAL"] = ItemType.realLiteral;
				TypeOfName["MACHINING_NC_FUNCTION"] = ItemType.machiningNcFunction;
				TypeOfName["MACHINING_PROCESS_EXECUTABLE"] = ItemType.machiningProcessExecutable;
				TypeOfName["INT_LITERAL"] = ItemType.intLiteral;
				TypeOfName["MACHINING_TOOLPATH_SPEED_PROFILE_REPRESENTATION"] = ItemType.machiningToolpathSpeedProfileRepresentation;
				TypeOfName["APPLIED_SECURITY_CLASSIFICATION_ASSIGNMENT"] = ItemType.appliedSecurityClassificationAssignment;
				TypeOfName["MARKER_TYPE"] = ItemType.markerType;
				TypeOfName["POSITIVE_LENGTH_MEASURE"] = ItemType.positiveLengthMeasure;
				TypeOfName["PARAMETER_VALUE"] = ItemType.parameterValue;
			}

			public void ResolveIndex(List<Item> definitions)
			{
				if (val is List<Item>)
				{
					for (int i = 0; i < (val as List<Item>).Count; i++)
					{
						if ((val as List<Item>)[i] != null && (val as List<Item>)[i].type == ItemType.index) (val as List<Item>)[i] = definitions[(int)((val as List<Item>)[i].val)];
					}
				}
				if (parameter != null)
				{
					string[] parnames = parameter.Keys.ToArray();
					foreach (string name in parnames)
					{
						if (parameter[name].type == ItemType.index) parameter[name] = definitions[(int)(parameter[name].val)];
						if (parameter[name].type == ItemType.list)
							for (int i = 0; i < (parameter[name].val as List<Item>).Count; i++)
							{
								(parameter[name].val as List<Item>)[i].ResolveIndex(definitions);
								if ((parameter[name].val as List<Item>)[i].type == ItemType.index) (parameter[name].val as List<Item>)[i] = definitions[(int)(parameter[name].val as List<Item>)[i].val];
							}
					}
				}
			}
			public void Resolve(List<Item> definitions, Dictionary<ItemType, List<int>> roots)
			{

				if (IsEntity)
				{
					string[] par = StepSyntax.GetAllParameters(StepSyntax.CamelCaseToStepDef(type.ToString()));
#if DEBUG
					if (StepSyntax.Parameter.ContainsKey(StepSyntax.CamelCaseToStepDef(type.ToString())) && (val as List<Item>).Count != par.Length) throw new ApplicationException("error resolving entity: " + type.ToString());
#endif
					for (int i = 0; i < par.Length; i++)
					{
						if ((val as List<Item>)[i] != null && (val as List<Item>)[i].type == ItemType.index) (val as List<Item>)[i] = definitions[(int)((val as List<Item>)[i].val)];
						parameter[par[i]] = (val as List<Item>)[i];
						//if ((val as List<Item>)[i].definingIndex == 0)
						if ((val as List<Item>)[i] != null) (val as List<Item>)[i].ResolveIndex(definitions);
					}
					// set val to null here, when usage of val in createEntity is changed to using parameters
					// but for now, we need a List<Item> to mark it as valid entity
				}
				else if (type == ItemType.list)
				{
					if ((val as List<Item>)[0].IsEntity)
					{   // here we have this strange concept in step files, often used with RATIONAL_B_SPLINE_SURFACE or GLOBAL_UNIT_ASSIGNED_CONTEXT
						// where for a single # entry there are several types and for each type only the final parameters are given, while the
						// parameters of subtypes are given in the same # entry but seperate type definitions
						// This seems to be necessary to combine different parameter sets into one entity.
						// We need to find the highest common subtype to use as the type of the entity and simply add all the parameters
						// since there are no name conflichts (hopefully!)
						List<Item> lst = val as List<Item>;
						Dictionary<string, int> finalTypes = new Dictionary<string, int>();
						for (int i = 0; i < lst.Count; i++)
						{
							string cctype = StepSyntax.CamelCaseToStepDef(lst[i].type.ToString());
							finalTypes[cctype] = 0; // usage count
													// now resolve only the type specific parameters, not the parameters of subtypes, which are not given
							if (StepSyntax.Parameter.TryGetValue(cctype, out string[] par))
							{
#if DEBUG
								if ((lst[i].val as List<Item>).Count != par.Length) throw new ApplicationException("error resolving entity: " + lst[i].type.ToString());
#endif
								lst[i].parameter = new Dictionary<string, Item>();
								for (int j = 0; j < par.Length; j++)
								{
									if ((lst[i].val as List<Item>)[j].type == ItemType.index) (lst[i].val as List<Item>)[j] = definitions[(int)((lst[i].val as List<Item>)[j].val)];
									lst[i].parameter[par[j]] = (lst[i].val as List<Item>)[j];
								}
							}
						}
						string[] fkeys = finalTypes.Keys.ToArray();
						foreach (string tp in fkeys)
						{
							if (StepSyntax.SubType.TryGetValue(tp, out string[] st))
							{
								for (int i = 0; i < st.Length; i++) finalTypes[st[i]] = finalTypes[st[i]] + 1;
							}
						}
						string commonSubType = null;
						int maxUsage = 0;
						foreach (KeyValuePair<string, int> kv in finalTypes)
						{
							if (kv.Value > maxUsage)
							{
								maxUsage = kv.Value;
								commonSubType = kv.Key;
							}
						}
						this.type = TypeOfName[commonSubType.ToUpper()];
						this.parameter = new Dictionary<string, Item>();
						for (int i = 0; i < lst.Count; i++)
						{
							if (lst[i].parameter != null)
							{
								foreach (KeyValuePair<string, Item> kv in lst[i].parameter)
								{
									this.parameter[kv.Key] = kv.Value;
								}
							}
						}
						foreach (Item item in parameter.Values)
						{
							if (item.type == ItemType.list)
							{
								for (int i = 0; i < item.lval.Count; i++)
								{
									if (item.lval[i].type == ItemType.index) item.lval[i] = definitions[item.lval[i].ival];
								}
							}
						}
						this.val = new List<Item>(); // better would be null, but for now, we need a List<Item> to mark it as valid entity
						if (roots.TryGetValue(this.type, out List<int> indices))
						{
							indices.Add(this.definingIndex);
						}
					}
				}

			}
			internal static ItemType GetType(string name)
			{
				ItemType res;
				if (!TypeOfName.TryGetValue(name, out res))
				{
					return ItemType.invalid; // should never happen
				}
				return res;
			}

			internal string SubString(int ind)
			{
				List<Item> lst = val as List<Item>;
				if (lst != null && lst.Count > ind)
				{
					if (lst[ind].type == ItemType.stringval || lst[ind].type == ItemType.keyword) return lst[ind].val as string;
				}
				return null;
			}

			internal double SubFloat(int ind)
			{
				List<Item> lst = val as List<Item>;
				if (lst != null && lst.Count > ind)
				{
					if (lst[ind].type == ItemType.floatval) return (double)lst[ind].val;
				}
				return 0.0;
			}

			internal bool SubBool(int ind)
			{
				List<Item> lst = val as List<Item>;
				if (lst != null && lst.Count > ind)
				{
					if (lst[ind].type == ItemType.keyword) return ((string)lst[ind].val == "T" || (string)lst[ind].val == "TRUE");
				}
				return false;
			}

			internal List<Item> SubList(int ind)
			{
				List<Item> lst = val as List<Item>;
				if (lst != null && lst.Count > ind)
				{
					if (lst[ind].type == ItemType.list) return lst[ind].val as List<Item>;
				}
				return null;
			}

			internal int SubIndex(int ind)
			{
				List<Item> lst = val as List<Item>;
				if (lst != null && lst.Count > ind)
				{
					if (lst[ind].type == ItemType.index) return (int)lst[ind].val;
				}
				return 0; // sollte es nie geben, also immer invalid sein
			}

			internal Item SubItem(int ind)
			{
				List<Item> lst = val as List<Item>;
				if (lst != null && lst.Count > ind)
				{
					return lst[ind];
				}
				return null;
			}

			public Item this[string par]
			{
				get
				{
					if (parameter.TryGetValue(par, out Item res)) return res;
					else return null;
				}
			}
			public List<Item> lval
			{
				get
				{
					return val as List<Item>;
				}
			}
			public bool bval
			{
				get
				{
					if (type == ItemType.keyword) return ((string)val == "T" || (string)val == "TRUE");
					return false;
				}
			}
			public string sval
			{
				get
				{
					if (val is string) return (string)val;
					return null;
				}
			}
			public int ival
			{
				get
				{
					if (val is int) return (int)val;
					else if (val is double) return (int)(double)val;
					else return 0;
				}
			}
			public double fval
			{
				get
				{
					if (val is int) return (int)val;
					else if (val is double) return (double)val;
					else return 0;
				}
			}
		}
		public class SyntaxError : ApplicationException
		{
			public SyntaxError(string line, int start, char expected) : base("Syntax error in step file: expected '" + expected + "' in line: " + line)
			{

			}
			public SyntaxError(string msg) : base(msg)
			{

			}
		}
		public ImportStep()
		{
			definitions = new List<Item>();
			importProblems = new Dictionary<int, string>();
			notImportedFaces = new List<Item>();
			nurbsSurfacesWithModifiedKnots = new System.Collections.Concurrent.ConcurrentDictionary<NurbsSurface, ModOp2D>();
#if DEBUG
			allNames = new SortedDictionary<string, int>();
			entityPattern = new Dictionary<string, HashSet<string>>();
#endif
			transformationMode = Settings.GlobalSettings.GetBoolValue("StepImport.Transformation", false);  // one of two ways to make the transformation, I cannot distinguish the cases when to use which

			// transformationMode = true;
			// for most files, transformationMode is not important.
			// files, for which transformationMode must be false: ?? FILE_SCHEMA(('AUTOMOTIVE_DESIGN { 1 0 10303 214 1 1 1 1 }'));
			// ATTREZZATURA + GIRANTE.stp, AL_1180775_AL_1180775.stp, SSSS4912PCAM.stp

			// Assembly2.step doesn't work correctly with both methods!
			Item.Init();
		}

		private void MakeRelations(List<int> cdsr)
		{
			// product_definition: 
			// "contextDependentShapeRepresentation(107)->productDefinitionShape(239691)->nextAssemblyUsageOccurrence(128)->productDefinition(239720)"
			// "shapeDefinitionRepresentation(239650)->productDefinitionShape(239670)->productDefinition(239720)"
			// shapeDefinitionRepresentation->ShapeRepresentation->manifoldSolidBrep
			// contextDependentShapeRepresentation legt parent -> child+transformation zwischen product_definitions fest
			// andererseits legt shapeDefinitionRepresentation die BREP Form von product_definition fest
			// product_definition
			// - children(product_definition) + transforms
			// - eigenes brep
			for (int i = 0; i < cdsr.Count; i++)
			{
				Item item = definitions[cdsr[i]];
			}
		}
		public Dictionary<int, string> Problems
		{
			get
			{
				return importProblems;
			}
		}
		/// <summary>
		/// Convert the hierarchy of objects into a hierarchy of <see cref="Block"/>s.
		/// </summary>
		public bool HierarchyToBlocks
		{
			set { makeBlocks = value; }
		}
		public GeoObjectList Read(string filename)
		{
			GeoObjectList res = new GeoObjectList();
			Dictionary<Item.ItemType, List<int>> roots = new Dictionary<Item.ItemType, List<int>>();
			// presentationLayerAssignment->advancedFace
			// shapeRepresentationRelationship->manifoldSurfaceShapeRepresentation->shellBasedSurfaceModel->openShell->advancedFace
			// shapeRepresentationRelationship->advancedBrepShapeRepresentation->manifoldSolidBrep->closedShell->advancedFace
			// presentationLayerAssignment->manifoldSolidBrep->closedShell->advancedFace
			// mechanicalDesignGeometricPresentationRepresentation->styledItem->manifoldSolidBrep->closedShell->advancedFace
			// mechanicalDesignGeometricPresentationRepresentation->styledItem->advancedFace
			// shapeDefinitionRepresentation->advancedBrepShapeRepresentation->manifoldSolidBrep->closedShell->advancedFace

			// typical unreferenced entities:
			// contextDependentShapeRepresentation
			// shapeRepresentationRelationship
			// shapeDefinitionRepresentation +
			// productRelatedProductCategory
			// presentationLayerAssignment
			// mechanicalDesignGeometricPresentationRepresentation

			roots[Item.ItemType.mechanicalDesignGeometricPresentationRepresentation] = new List<int>(); // das scheint mir der root zu sein
			roots[Item.ItemType.shapeDefinitionRepresentation] = new List<int>(); // manchmal auch das
			roots[Item.ItemType.presentationStyleAssignment] = new List<int>();
			roots[Item.ItemType.presentationLayerAssignment] = new List<int>();
			roots[Item.ItemType.shapeRepresentationRelationship] = new List<int>();
			roots[Item.ItemType.representationRelationship] = new List<int>();
			roots[Item.ItemType.contextDependentShapeRepresentation] = new List<int>();
			roots[Item.ItemType.draughtingModel] = new List<int>();
			roots[Item.ItemType.styledItem] = new List<int>();
			roots[Item.ItemType.itemDefinedTransformation] = new List<int>();
			roots[Item.ItemType.constructiveGeometryRepresentationRelationship] = new List<int>();
			Set<Item> productDefinitions = new Set<Item>();

			using (tk = new Tokenizer(filename))
			{
				try
				{
					FrameImpl.MainFrame?.UIService.ShowProgressBar(true, 0.0, "importing step file");
					while (!tk.EndOfFile)
					{
						if (!Statement(roots)) throw new SyntaxError("general syntax error reading step file");
					}
					createdFaces = numFaces = 0;
					for (int i = 0; i < definitions.Count; i++)
					{
						if (definitions[i] != null)
						{
							definitions[i].definingIndex = i;
							definitions[i].Resolve(definitions, roots);
							if (definitions[i].type == Item.ItemType.productDefinition) productDefinitions.Add(definitions[i]);
							if (definitions[i].type == Item.ItemType.advancedFace || definitions[i].type == Item.ItemType.faceSurface
								|| definitions[i].type == Item.ItemType.curveBoundedSurface) ++numFaces; // for progress
						}
					}
#if DEBUG
					Set<Item> allRootItems = new Set<Item>(definitions);

					definitionStack = new Stack<int>();
					for (int i = 0; i < definitions.Count; i++)
					{
						if (definitions[i] != null)
						{
							if (definitions[i].parameter != null && definitions[i].parameter.Count > 0)
							{
								foreach (KeyValuePair<string, Item> kv in definitions[i].parameter)
								{
									kv.Value.SetUsedBy(definitions[i]);
									allRootItems.Remove(kv.Value);
								}
							}
							else if (definitions[i].val is List<Item>)
							{
								for (int j = 0; j < (definitions[i].val as List<Item>).Count; j++)
								{
									(definitions[i].val as List<Item>)[j].SetUsedBy(definitions[i]);
									allRootItems.Remove((definitions[i].val as List<Item>)[j]);
								}
							}
							//if (definitions[i].parameter != null && definitions[i].parameter.TryGetValue("id", out Item idItem))
							//{
							//    System.Diagnostics.Trace.WriteLine("Item with id: " + idItem.sval + ": " + definitions[i].type.ToString() + ", " + definitions[i].definingIndex.ToString());

							//}
							//if (definitions[i].parameter != null && definitions[i].parameter.TryGetValue("name", out Item nameItem) && nameItem.sval.Length > 0)
							//{
							//    System.Diagnostics.Trace.WriteLine("Item with name: " + nameItem.sval + ": " + definitions[i].type.ToString() + ", " + definitions[i].definingIndex.ToString());

							//}
						}
					}
					Set<string> allRootTypes = new Set<string>();
					foreach (Item item in allRootItems)
					{
						if (item != null)
							allRootTypes.Add(item.type.ToString());
					}
					System.Diagnostics.Trace.WriteLine("Root types:");
					foreach (string typeName in allRootTypes)
					{
						System.Diagnostics.Trace.WriteLine(typeName);
					}
					//string[] dbg = UsagePaths(definitions[19189]);
					//foreach (Item item in allRootItems)
					//{   // try to dump the hierarchical structure of this step file
					//    if (item != null)
					//    {
					//        string sdbg = SubTree(item.definingIndex, 6);
					//        if (!string.IsNullOrEmpty(sdbg)) System.Diagnostics.Trace.WriteLine(sdbg);
					//    }
					//}
					//string[] dbg = UsagePaths(definitions[3275]);
					//for (int i = 0; i < dbg.Length; i++)
					//{
					//    System.Diagnostics.Trace.WriteLine(dbg[i]);
					//}
					// nur zum merken: shapeRepresentationRelationship führt einerseits zu *shapeRepresentation*, andererseits über advancedBrepShapeRepresentation zum Objekt
					// *shapeRepresentation* muss sich das Objekt merken, denn contextDependentShapeRepresentation(5490)->representationRelationship(5489)->shapeRepresentation(5473)
					// ist das Einfügen selbst. Das sind die wichtigen roots: shapeDefinitionRepresentation, contextDependentShapeRepresentation, shapeRepresentationRelationship
#endif
					//Settings.GlobalSettings.SetValue("StepImport.Parallel", false);
					if (Settings.GlobalSettings.GetBoolValue("StepImport.Parallel", true))
					{
						Parallel.For(0, roots[Item.ItemType.shapeDefinitionRepresentation].Count, i =>
						{
							Item item = definitions[roots[Item.ItemType.shapeDefinitionRepresentation][i]];
							object so = CreateEntity(item);
						});
					}
					else
					{
						for (int i = 0; i < roots[Item.ItemType.shapeDefinitionRepresentation].Count; i++)
						{
							Item item = definitions[roots[Item.ItemType.shapeDefinitionRepresentation][i]];
							object so = CreateEntity(item);
						}
					}
					for (int i = 0; i < roots[Item.ItemType.representationRelationship].Count; i++)
					{
						Item item = definitions[roots[Item.ItemType.representationRelationship][i]];
						ModOp tranMop = ModOp.Identity;
						if (item.parameter.TryGetValue("transformation_operator", out Item transformation))
						{
							object o = CreateEntity(transformation);
							if (o is ModOp t) tranMop = t;
						}
						GeoObjectList go = CreateEntity(item) as GeoObjectList;
						string name = null;
						if (item.parameter["rep_1"].parameter.TryGetValue("name", out Item shapeRepresentation)) name = shapeRepresentation.sval;
						object dbg1 = CreateEntity(item.parameter["rep_1"]);
						object dbg2 = CreateEntity(item.parameter["rep_2"]);
						if (item.parameter["rep_2"].val is GeoObjectList gol)
						{
							if (gol.Count > 1)
							{
								Block blk = Block.Construct();
								if (string.IsNullOrWhiteSpace(name)) name = $"Item {item.definingIndex}";
								blk.Name = name;
								blk.Set(gol.CloneObjects());
								res.Add(blk);
							}
							else
							{
								if (!string.IsNullOrWhiteSpace(name))
								{
									for (int j = 0; j < gol.Count; j++)
									{
										if (gol[j] is Solid sld) sld.Name = name;
										if (gol[j] is Shell shl) shl.Name = name;
									}
								}
								res.AddRange(gol);
							}
						}
						else if (item.parameter["rep_1"].val is GeoObjectList gol1)
						{
							if (gol1.Count > 1)
							{
								Block blk = Block.Construct();
								if (string.IsNullOrWhiteSpace(name)) name = $"Item {item.definingIndex}";
								blk.Name = name;
								blk.Set(gol1.CloneObjects());
								res.Add(blk);
							}
							else
							{
								if (!string.IsNullOrWhiteSpace(name))
								{
									for (int j = 0; j < gol1.Count; j++)
									{
										if (gol1[j] is Solid sld) sld.Name = name;
										if (gol1[j] is Shell shl) shl.Name = name;
									}
								}
								res.AddRange(gol1);
							}
						}
					}
					for (int i = 0; i < roots[Item.ItemType.mechanicalDesignGeometricPresentationRepresentation].Count; i++)
					{   // we do this before we do the shapeRepresentationRelationship iteration, because here the colors are set and in the next loop the objects are cloned
						Item item = definitions[roots[Item.ItemType.mechanicalDesignGeometricPresentationRepresentation][i]];
						GeoObjectList go = CreateEntity(item) as GeoObjectList;
					}
					for (int i = 0; i < roots[Item.ItemType.constructiveGeometryRepresentationRelationship].Count; i++)
					{   // we do this before we do the shapeRepresentationRelationship iteration, because here the colors are set and in the next loop the objects are cloned
						Item item = definitions[roots[Item.ItemType.constructiveGeometryRepresentationRelationship][i]];
						GeoObjectList go = CreateEntity(item) as GeoObjectList;
#if DEBUG
						//System.Diagnostics.Trace.WriteLine(SubTree(item.definingIndex, 5));
#endif
						string name = null;
						if (item.parameter["rep_1"].parameter.TryGetValue("name", out Item shapeRepresentation)) name = shapeRepresentation.sval;
						// I do not understand the semantics of the STEP file in respect to shapeRepresentationRelationship, representationRelationship, shapeRepresentation and so on...
						// in file 1211_EDS-EL.stp there are some representationRelationship items and some shapeRepresentationRelationship items which have identical "rep_1" parameters
						// but the representationRelationship items don't refer to a geometry and the shapeRepresentationRelationship items don't refer to a transformation. In other files
						// this connection is build by the product, productDefinition items, but here we combine the shapeRepresentationRelationship items and the (via "rep_1") corresponding
						// representationRelationship items to have both the geometry and the transformation.
						ModOp tranMop = ModOp.Identity;
						Item transformation = null;
						if (!item.parameter.TryGetValue("transformation_operator", out transformation))
						{
							item.parameter["rep_1"].parameter.TryGetValue("transformation_operator", out transformation);
						}
						if (transformation != null)
						{
							object o = CreateEntity(transformation);
							if (o is ModOp)
							{
								tranMop = (ModOp)o;
							}
						}
						if (item.parameter["rep_2"].val is GeoObjectList gol)
						{
							if (gol.Count > 1)
							{
								Block blk = Block.Construct();
								if (string.IsNullOrWhiteSpace(name)) name = $"Item {item.definingIndex}";
								blk.Name = name;
								blk.Set(gol.CloneObjects());
								if (!tranMop.IsIdentity(0.0)) blk.Modify(tranMop);
								res.Add(blk);
							}
							else
							{   // we could also make a block here with a single solid
								if (!string.IsNullOrWhiteSpace(name))
								{
									if (gol[0] is Solid sld) sld.Name = name;
									if (gol[0] is Shell shl) shl.Name = name;
								}
								IGeoObject clone = gol[0].Clone();
								if (!tranMop.IsIdentity(0.0)) clone.Modify(tranMop);
								res.Add(clone);
							}
						}
					}
					for (int i = 0; i < roots[Item.ItemType.styledItem].Count; i++)
					{   // in most cases the styled items are already created (e.g. as part of mechanicalDesignGeometricPresentationRepresentation), but in some files (e.g. SSSS4912PCAM.stp) 
						// there are unreferenced styledItems. Upon creation the objects referenced by the styled item are styled (color set)
						Item item = definitions[roots[Item.ItemType.styledItem][i]];
						object o = CreateEntity(item);
					}
					for (int i = 0; i < roots[Item.ItemType.shapeRepresentationRelationship].Count; i++)
					{
						Item item = definitions[roots[Item.ItemType.shapeRepresentationRelationship][i]];
						GeoObjectList go = CreateEntity(item) as GeoObjectList;
#if DEBUG
						//System.Diagnostics.Trace.WriteLine(SubTree(item.definingIndex, 5));
#endif
						string name = null;
						if (item.parameter["rep_1"].parameter.TryGetValue("name", out Item shapeRepresentation)) name = shapeRepresentation.sval;
						// I do not understand the semantics of the STEP file in respect to shapeRepresentationRelationship, representationRelationship, shapeRepresentation and so on...
						// in file 1211_EDS-EL.stp there are some representationRelationship items and some shapeRepresentationRelationship items which have identical "rep_1" parameters
						// but the representationRelationship items don't refer to a geometry and the shapeRepresentationRelationship items don't refer to a transformation. In other files
						// this connection is build by the product, productDefinition items, but here we combine the shapeRepresentationRelationship items and the (via "rep_1") corresponding
						// representationRelationship items to have both the geometry and the transformation.
						ModOp tranMop = ModOp.Identity;
						Item transformation = null;
						if (!item.parameter.TryGetValue("transformation_operator", out transformation))
						{
							item.parameter["rep_1"].parameter.TryGetValue("transformation_operator", out transformation);
						}
						if (transformation != null)
						{
							object o = CreateEntity(transformation);
							if (o is ModOp)
							{
								tranMop = (ModOp)o;
							}
						}
						if (item.parameter["rep_2"].val is GeoObjectList gol)
						{
							if (gol.Count > 1)
							{
								Block blk = Block.Construct();
								if (string.IsNullOrWhiteSpace(name)) name = $"Item {item.definingIndex}";
								blk.Name = name;
								blk.Set(gol.CloneObjects());
								if (!tranMop.IsIdentity(0.0)) blk.Modify(tranMop);
								res.Add(blk);
							}
							else
							{   // we could also make a block here with a single solid
								if (!string.IsNullOrWhiteSpace(name))
								{
									if (gol[0] is Solid sld) sld.Name = name;
									if (gol[0] is Shell shl) shl.Name = name;
								}
								IGeoObject clone = gol[0].Clone();
								if (!tranMop.IsIdentity(0.0)) clone.Modify(tranMop);
								res.Add(clone);
							}
						}
					}
					for (int i = 0; i < roots[Item.ItemType.contextDependentShapeRepresentation].Count; i++)
					{
						Item item = definitions[roots[Item.ItemType.contextDependentShapeRepresentation][i]];
						GeoObjectList go = CreateEntity(item) as GeoObjectList;
					}
					for (int i = 0; i < roots[Item.ItemType.presentationLayerAssignment].Count; i++)
					{
						Item item = definitions[roots[Item.ItemType.presentationLayerAssignment][i]];
						object o = CreateEntity(item);
						if (o is Pair<Layer, GeoObjectList>)
						{
							Layer layer = ((Pair<Layer, GeoObjectList>)o).First;
							GeoObjectList list = ((Pair<Layer, GeoObjectList>)o).Second;
							for (int j = 0; j < list.Count; j++)
							{
								list[j].Layer = layer;
							}
						}
					}
					for (int i = 0; i < roots[Item.ItemType.draughtingModel].Count; i++)
					{
						Item item = definitions[roots[Item.ItemType.draughtingModel][i]];
						object o = CreateEntity(item);
					}
					for (int i = 0; i < roots[Item.ItemType.itemDefinedTransformation].Count; i++)
					{
						Item item = definitions[roots[Item.ItemType.itemDefinedTransformation][i]];
						object o = CreateEntity(item);
					}

					// there is the concept of "mappedItem", which is not respected with the "products" concept.
					// I am not sure how to handle this, so I am doing this as exclusive patterns. If we have mappedItems, we do not use the products.
					// The mappedItems seem also to appear in the products.
					HashSet<Item> rootMappedItems = new HashSet<Item>(mappedItems);
					if (mappedItems.Count > 0)
					{
						foreach (Item mpItem in mappedItems)
						{
							foreach (Item subitem in mpItem["mapping_source"]["mapped_representation"]["items"].lval)
							{
								if (subitem.type == Item.ItemType.mappedItem) rootMappedItems.Remove(subitem);
							}
						}
					}
					GeoObjectList rmiList = new GeoObjectList();
					if (rootMappedItems.Count > 0)
					{
						foreach (Item mi in rootMappedItems)
						{
							object l = CreateEntity(mi);
							if (l is GeoObjectList) rmiList.AddRange(l as GeoObjectList);
							if (l is IGeoObject) rmiList.Add(l as IGeoObject); // must be a Block 
						}
					}
					if (rootMappedItems.Count > 0 && rmiList.Count > 0)
					{
						res = rmiList;
					}
					else
					{
						foreach (Item product in products)
						{
							CreateProduct(product);
						}
						HashSet<Item> rootProducts = new HashSet<Item>(products);
						foreach (Item product in products)
						{
							if (product.parameter.TryGetValue("_children", out Item children))
							{
								for (int j = 0; j < children.lval.Count; j++)
								{
									rootProducts.Remove(children.lval[j].parameter["_referred"]);
								}
							}
						}
						if (rootProducts.Count > 0)
						{
							GeoObjectList prodlist = new GeoObjectList();
							foreach (Item rootProduct in rootProducts)
							{
#if DEBUG
								System.Diagnostics.Trace.WriteLine(ProductHierarchie(rootProduct, ""));
								Block blk = ProductToBlock(rootProduct);
#endif
								if (rootProduct.parameter.ContainsKey("_geo"))
								{
									GeoObjectList l = rootProduct.parameter["_geo"].val as GeoObjectList;
									if (l != null)
									{
										if (l.Count > 1 && makeBlocks)
										{
											Block prodblk = Block.Construct();
											prodblk.Name = rootProduct.parameter["name"].sval;
											prodblk.Set(l);
											prodlist.Add(prodblk);
										}
										else
										{
											prodlist.AddRange(l);
										}
									}
								}
							}
							if (prodlist.Count > 0) res = prodlist;
						}
					}
					if (res == null || res.Count == 0)
					{
						if (res == null) res = new GeoObjectList();
						for (int i = 0; i < roots[Item.ItemType.mechanicalDesignGeometricPresentationRepresentation].Count; i++)
						{
							Item item = definitions[roots[Item.ItemType.mechanicalDesignGeometricPresentationRepresentation][i]];
							if (item.val is GeoObjectList l)
							{
								res.AddRange(l);
							}
						}
					}
				}
				catch (SyntaxError e)
				{
#if DEBUG
					System.Diagnostics.Trace.WriteLine(e.Message);
#endif
					importProblems[0] = "SyntaxError: " + e.Message;
				}
			}
			FrameImpl.MainFrame?.UIService.ShowProgressBar(false);
#if DEBUG
			System.Diagnostics.Trace.WriteLine("Finished step read" + Environment.TickCount.ToString());
#endif
#if DEBUG
			if (!res.CheckConsistency()) { }
			BoundingCube ext = res.GetExtent();
			double md = 0.0;
			Face found = null;
			for (int i = 0; i < res.Count; i++)
			{
				Shell shell = null;
				if (res[i] is Shell sh) shell = sh;
				if (res[i] is Solid sld) shell = sld.Shells[0];
				if (shell != null)
				{
					if (!shell.CheckConsistency())
					{

					}
					foreach (Face face in shell.Faces)
					{
						foreach (Vertex vtx in face.Vertices)
						{
							double d = Math.Abs(face.Surface.GetDistance(vtx.Position));
							if (d > md)
							{
								md = d;
								found = face;
							}
						}
						GeoPoint[] vtxs = new GeoPoint[face.Vertices.Length];
						for (int ii = 0; ii < vtxs.Length; ii++)
						{
							vtxs[ii] = face.Vertices[ii].Position;
						}
						//double err1 = face.Surface.Fit(vtxs); // implement Modify in surfaces!
					}
				}
			}
#endif
			if (!Settings.GlobalSettings.GetBoolValue("StepImport.Blocks", true)) res.DecomposeBlocks();
			return res;
		}
#if DEBUG
		//public SortedDictionary<string, GeoObjectList> allObjects = new SortedDictionary<string, GeoObjectList>();
		string ProductHierarchie(Item product, string indent)
		{
			StringBuilder res = new StringBuilder();
			res.AppendLine(indent + product.parameter["id"].sval);
			if (product.parameter.TryGetValue("_children", out Item children))
			{
				for (int i = 0; i < children.lval.Count; i++)
				{
					res.Append(ProductHierarchie(children.lval[i].parameter["_referred"], indent + "---"));
				}
			}
			return res.ToString();
		}
#endif
		Block ProductToBlock(Item product)
		{
			Block blk = Block.Construct();
			GeoObjectList collect = new GeoObjectList();
			// if (product.parameter["_geo"].val is GeoObjectList list) collect.AddRange(list);
			blk.Name = product.parameter["id"].sval;
			if (product.parameter.TryGetValue("_children", out Item children))
			{
				for (int i = 0; i < children.lval.Count; i++)
				{
					Block blksub = ProductToBlock(children.lval[i].parameter["_referred"]);
					if (blksub.NumChildren > 0)
					{
						for (int j = 0; j < blksub.Count; j++)
						{
							collect.Remove(blksub.Child(j));
						}
						collect.Add(blksub);
					}
					//if (blksub.NumChildren == 1)
					//{
					//    IGeoObject ch = blksub.Child(0);
					//    blksub.Remove(0);
					//    collect.Add(ch);
					//}
					//else if (blksub.NumChildren > 1)
					//{
					//    collect.Add(blksub);
					//}
				}
				blk.Set(collect);
			}
			else
			{
				if (product.parameter["_geo"].val is GeoObjectList list) collect.AddRange(list);
				blk.Set(collect);
			}
			return blk;
		}
		private void CreateProduct(Item product)
		{
			//System.Diagnostics.Trace.WriteLine(SubTree(product.definingIndex, 5));
			//System.Diagnostics.Trace.Write(product.parameter["id"].sval + ": ");
			//for (int i = 0; i < product.parameter["frame_of_reference"].lval.Count; i++)
			//{
			//    System.Diagnostics.Trace.Write(product.parameter["frame_of_reference"].lval[i].type.ToString() + ", ");
			//}
			//System.Diagnostics.Trace.WriteLine("");
			if (!product.parameter.ContainsKey("_geo"))
			{
				// System.Diagnostics.Trace.WriteLine("CreateProduct: " + product.definingIndex.ToString());
				GeoObjectList res = new GeoObjectList();
				product.parameter["_geo"] = new Item(Item.ItemType.created, res);
				if (product.parameter.TryGetValue("_children", out Item children))
				{
					for (int i = 0; i < children.lval.Count; i++)
					{
						ModOp m = (ModOp)children.lval[i].parameter["_transformation"].val;
						CreateProduct(children.lval[i].parameter["_referred"]);
						GeoObjectList list = children.lval[i].parameter["_referred"].parameter["_geo"].val as GeoObjectList;
						Block blk = Block.Construct();
						blk.Name = children.lval[i].parameter["_referred"].parameter["name"].sval;
						HashSet<(IGeoObject, ModOp)> alreadyAdded = new HashSet<(IGeoObject, ModOp)>();
						if (list.Count > 1 && makeBlocks)
						{
							for (int j = 0; j < list.Count; j++)
							{
								if (alreadyAdded.Contains((list[j], m))) continue; // avoid adding the same object at the same position multiple times
								alreadyAdded.Add((list[j], m));
								IGeoObject sub = list[j].Clone();
								sub.Modify(m);
								if (Settings.GlobalSettings.GetBoolValue("StepImport.AddLocation", true)) sub.UserData.Add("STEP.Location", m);
								blk.Add(sub);
							}
							if (blk.NumChildren > 0) res.Add(blk);
						}
						else
						{
							for (int j = 0; j < list.Count; j++)
							{
								if (alreadyAdded.Contains((list[j], m))) continue; // avoid adding the same object at the same position multiple times
								alreadyAdded.Add((list[j], m));
								IGeoObject sub = list[j].Clone();
								sub.Modify(m);
								if (Settings.GlobalSettings.GetBoolValue("StepImport.AddLocation", true)) sub.UserData.Add("STEP.Location", m);
								res.Add(sub);
							}
						}
					}
				}
				if (product.parameter.TryGetValue("_context", out Item context))
				{
					string name = product.parameter["id"].sval;
					bool hasRelationship = false;
					for (int j = 0; j < context.lval.Count; j++)
					{
						if (context.lval[j].parameter.TryGetValue("_relationship", out Item rel))
						{
							hasRelationship = true;
							bool found = false;
							for (int k = 0; k < rel.lval.Count; k++)
							{
								Object o = CreateEntity(rel.lval[k]);
								if (o is IGeoObject)
								{
									res.Add(o as IGeoObject);
									found = true;
								}
								else if (o is GeoObjectList gl)
								{
									if (gl.Count > 1)
									{
										Block blk = Block.Construct();
										blk.Add(gl);
										if (!string.IsNullOrWhiteSpace(name)) blk.Name = name;
										res.Add(blk);
									}
									else
									{
										res.AddRange(gl);
									}
									found = true;
								}
								if (!string.IsNullOrWhiteSpace(name))
								{
									if (o is Solid sld) sld.Name = name; // was "&& string.IsNullOrWhiteSpace(sld.Name)" before, so here we overwrite the shapes name
									if (o is Shell shl) shl.Name = name; // && string.IsNullOrWhiteSpace(shl.Name)
								}
							}
							if (!found && namedGeoObjects.TryGetValue(name, out IGeoObject go)) res.Add(go);
						}
					}
					if (!hasRelationship && product.parameter.TryGetValue("_association", out Item assoc))
					{
						for (int k = 0; k < assoc.lval.Count; k++)
						{
							Object o = CreateEntity(assoc.lval[k]);
							if (o is IGeoObject) res.AddUnique(o as IGeoObject);
							else if (o is GeoObjectList) res.AddRange(o as GeoObjectList);
							if (!string.IsNullOrWhiteSpace(name))
							{
								if (o is Solid sld && string.IsNullOrWhiteSpace(sld.Name)) sld.Name = name;
								if (o is Shell shl && string.IsNullOrWhiteSpace(shl.Name)) shl.Name = name;
							}
						}
					}
				}
				//if (product.parameter.TryGetValue("_association", out Item assoc))
				//{
				//    string name = product.parameter["id"].sval;
				//    for (int i = 0; i < assoc.lval.Count; i++)
				//    {
				//        if (assoc.lval[i].type == Item.ItemType.axis2Placement_3d)
				//        {
				//            if (assoc.lval[i].parameter.TryGetValue("_relationship", out Item rel))
				//            {
				//                for (int j = 0; j < rel.lval.Count; j++)
				//                {
				//                    Object o = CreateEntity(rel.lval[j]);
				//                    if (o is IGeoObject) res.Add(o as IGeoObject);
				//                    else if (o is GeoObjectList) res.AddRange(o as GeoObjectList);
				//                    if (o is Solid) (o as Solid).Name = name;
				//                    if (o is Shell) (o as Shell).Name = name;
				//                }
				//            }
				//        }
				//        else
				//        {
				//            Object o = CreateEntity(assoc.lval[i]);
				//            if (o is IGeoObject) res.Add(o as IGeoObject);
				//            else if (o is GeoObjectList) res.AddRange(o as GeoObjectList);
				//            if (o is Solid) (o as Solid).Name = name;
				//            if (o is Shell) (o as Shell).Name = name;
				//        }
				//    }
				//}
				//System.Diagnostics.Trace.WriteLine("Created: " + product.definingIndex.ToString());
			}
		}
#if DEBUG
		private void SetUsedBy(int ind, List<Item> list)
		{
			if (list == null) return;
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].type == Item.ItemType.index) list[i].usedBy.Add(definitions[ind]);
				//if (list[i].type == Item.ItemType.list) SetUsedBy(ind, list[i].val as List<Item>);
				if (list[i].IsEntity) SetUsedBy(ind, list[i].val as List<Item>);
			}
		}
		private string[] UsagePaths(Item item, HashSet<Item> alreadyUsed = null)
		{
			if (alreadyUsed == null) alreadyUsed = new HashSet<Item>();
			List<string> res = new List<string>();
			for (int i = 0; i < item.usedBy.Count; i++)
			{
				if (alreadyUsed.Contains(item.usedBy[i])) continue;
				alreadyUsed.Add(item.usedBy[i]);
				string[] sub = UsagePaths(item.usedBy[i], alreadyUsed);
				if (sub.Length > 0)
				{
					for (int j = 0; j < sub.Length; j++)
					{
						sub[j] = sub[j] + "->" + item.type.ToString() + "(" + item.definingIndex.ToString() + ")";
					}
					res.AddRange(sub);
				}
			}
			if (item.usedBy.Count == 0) res.Add(item.type.ToString() + "(" + item.definingIndex.ToString() + ")"); // a root
			return res.ToArray();
		}
		Set<Item.ItemType> ignoreInSubTree;

		private string SubTree(Item item, int deepth, string name, string prefix, Set<Item> used)
		{
			if (ignoreInSubTree.Contains(item.type)) return "";
			StringBuilder res = new StringBuilder();
			if (item.type == Item.ItemType.axis2Placement_3d)
			{
			}
			if (!used.Contains(item))
			{
				used.Add(item);
				if (item.val is string)
				{
					res.Append(prefix + name + ": " + item.sval + Environment.NewLine);
				}
				else if (item.val is int || item.val is double)
				{
					res.Append(prefix + name + ": " + item.val.ToString() + Environment.NewLine);
				}
				else if (item.val is FreeCoordSys fcs)
				{
					res.Append(prefix + name + ": " + item.val.ToString() + " (" + item.definingIndex + ")" + Environment.NewLine);
				}
				else
				{
					res.Append(prefix + name + ": " + item.type.ToString() + "(" + item.definingIndex + ")" + Environment.NewLine);
					if (deepth > 0)
					{
						if (item.parameter != null && item.parameter.Count > 0)
						{
							foreach (KeyValuePair<string, Item> par in item.parameter)
							{
								res.Append(SubTree(par.Value, deepth - 1, par.Key, prefix + "   ", used));
							}
						}
						else if (item.lval != null && item.lval.Count > 0)
						{
							for (int i = 0; i < item.lval.Count; i++)
							{
								res.Append(SubTree(item.lval[i], deepth - 1, i.ToString(), prefix + "   ", used));
							}
						}
					}
				}
			}
			else
			{
				res.Append(prefix + name + ": " + item.type.ToString() + "--->(" + item.definingIndex + ")" + Environment.NewLine);
			}
			return res.ToString();
		}
		private string SubTree(int itemId, int deepth)
		{
			ignoreInSubTree = new Set<Item.ItemType>(); // break the subtree debug at these types:
			ignoreInSubTree.Add(Item.ItemType.advancedFace);
			ignoreInSubTree.Add(Item.ItemType.closedShell);
			ignoreInSubTree.Add(Item.ItemType.openShell);
			ignoreInSubTree.Add(Item.ItemType.orientedClosedShell);

			Item item = definitions[itemId];
			// these should not be root items, but sometimes are:
			if (item.type == Item.ItemType.plane) return "";
			if (item.type == Item.ItemType.faceOuterBound) return "";
			if (item.type == Item.ItemType.fillAreaStyleColour) return "";
			if (item.type == Item.ItemType.orientedEdge) return "";
			if (item.type == Item.ItemType.dimensionalExponents) return "";
			if (item.type == Item.ItemType.namedUnit) return "";
			if (item.type == Item.ItemType.itemDefinedTransformation) return "";
			if (item.type == Item.ItemType.uncertaintyMeasureWithUnit) return "";
			if (item.type == Item.ItemType.advancedFace) return "";
			if (item.type == Item.ItemType.surfaceStyleUsage) return "";
			if (item.type == Item.ItemType.overRidingStyledItem) return "";
			if (item.type == Item.ItemType.surfaceStyleFillArea) return "";
			if (item.type == Item.ItemType.faceBound) return "";
			if (item.type == Item.ItemType.cartesianPoint) return "";

			return SubTree(definitions[itemId], deepth, "root", "", new Set<Item>());
		}
		private HashSet<Item> ReferencedBy(Item item)
		{
			HashSet<Item> res = new HashSet<Item>();
			if (item.val is List<Item>)
			{
				foreach (Item it in (item.val as List<Item>))
				{
					if (!res.Contains(it))
					{
						res.Add(it);
						res.UnionWith(ReferencedBy(it));
					}
				}
			}
			if (item.parameter != null)
			{
				foreach (Item it in (item.parameter.Values))
				{
					if (!res.Contains(it))
					{
						res.Add(it);
						res.UnionWith(ReferencedBy(it));
					}
				}
			}
			return res;
		}
		private void UsedBy(int toTest, HashSet<int> usedBy)
		{
			if (toTest <= 0) return;
			foreach (Item item in definitions[toTest].usedBy)
			{
				if (usedBy.Add(item.definingIndex)) UsedBy(item.definingIndex, usedBy);
			}
		}
		private string ReferencedBy(params int[] items)
		{
			StringBuilder res = new StringBuilder();
			HashSet<Item> all = new HashSet<Item>();
			all = ReferencedBy(definitions[items[0]]);
			for (int i = 1; i < items.Length; i++)
			{
				all.IntersectWith(ReferencedBy(definitions[items[i]]));
			}
			foreach (Item item in all)
			{
				if (item.definingIndex > 0) res.Append(item.definingIndex.ToString() + ", ");
			}
			return res.ToString();
		}
		private string[] TReferencedBy(int item)
		{
			HashSet<Item> all = new HashSet<Item>();
			all = ReferencedBy(definitions[item]);
			List<string> res = new List<string>();
			foreach (Item it in all)
			{
				if (it.definingIndex != 0)
				{
					res.Add(it.type.ToString() + "(" + it.definingIndex.ToString() + ")");
				}
				else if (it.type == Item.ItemType.stringval) res.Add("'" + it.sval + "'");
			}
			return res.ToArray();
		}
		private string[] CommonReferencedItems(int id1, int id2)
		{
			HashSet<Item> all1 = ReferencedBy(definitions[id1]);
			HashSet<Item> all2 = ReferencedBy(definitions[id2]);
			all1.IntersectWith(all2);
			List<string> res = new List<string>();
			foreach (Item it in all1)
			{
				if (it.definingIndex != 0)
				{
					res.Add(it.type.ToString() + "(" + it.definingIndex.ToString() + ")");
				}
			}
			return res.ToArray();
		}
		private string[] CommonAncestors(int id1, int id2)
		{
			HashSet<int> all1 = new HashSet<int>();
			HashSet<int> all2 = new HashSet<int>();
			UsedBy(id1, all1);
			UsedBy(id2, all2);
			all1.IntersectWith(all2);
			List<string> res = new List<string>();
			foreach (int i in all1)
			{
				if (definitions[i].definingIndex != 0)
				{
					res.Add(definitions[i].type.ToString() + "(" + definitions[i].definingIndex.ToString() + ")");
				}
			}
			return res.ToArray();
		}
		private string[] AllAncestors(int id1)
		{
			HashSet<int> all1 = new HashSet<int>();
			UsedBy(id1, all1);
			List<string> res = new List<string>();
			foreach (int i in all1)
			{
				if (i <= 0) continue;
				if (definitions[i].definingIndex != 0)
				{
					res.Add(definitions[i].type.ToString() + "(" + definitions[i].definingIndex.ToString() + ")");
				}
			}
			return res.ToArray();
		}
		private string[] RootItems
		{
			get
			{
				List<string> res = new List<string>();
				for (int i = 0; i < definitions.Count; i++)
				{
					if (definitions[i] != null && definitions[i].usedBy.Count == 0) res.Add(definitions[i].type.ToString() + " (" + i.ToString() + ")");
				}
				return res.ToArray();
			}
		}
#endif
		private object CreateGeoPointList(List<Item> lst)
		{
			List<GeoPoint> res = new List<GeoPoint>();
			List<GeoPoint2D> res2d = new List<GeoPoint2D>();
			for (int i = 0; i < lst.Count; i++)
			{
				object o = CreateEntity(lst[i]);
				if (o is GeoPoint) res.Add((GeoPoint)o);
				if (o is GeoPoint2D) res2d.Add((GeoPoint2D)o);
			}
			if (res2d.Count > 0) return res2d;
			return res;
		}
		private List<List<GeoPoint>> CreateGeoPointList2(List<Item> lst)
		{
			List<List<GeoPoint>> res = new List<List<GeoPoint>>();
			for (int i = 0; i < lst.Count; i++)
			{
				if (lst[i].type == Item.ItemType.list)
				{
					if (CreateGeoPointList(lst[i].val as List<Item>) is List<GeoPoint> l) res.Add(l); // no 2d lists here?
				}
			}
			return res;
		}
		private List<int> CreateIntList(List<Item> lst)
		{
			List<int> res = new List<int>();
			for (int i = 0; i < lst.Count; i++)
			{
				if (lst[i].type == Item.ItemType.floatval) res.Add((int)(double)lst[i].val);
			}
			return res;
		}
		private List<List<int>> CreateIntList2(List<Item> lst)
		{
			List<List<int>> res = new List<List<int>>();
			for (int i = 0; i < lst.Count; i++)
			{
				object o = CreateEntity(lst[i]);
				if (o is List<int>) res.Add(o as List<int>);
			}
			return res;
		}
		private List<double> CreateFloatList(List<Item> lst)
		{
			List<double> res = new List<double>();
			for (int i = 0; i < lst.Count; i++)
			{
				if (lst[i].type == Item.ItemType.floatval) res.Add((double)lst[i].val);
			}
			return res;
		}
		private List<List<double>> CreateFloatList2(List<Item> lst)
		{
			List<List<double>> res = new List<List<double>>();
			for (int i = 0; i < lst.Count; i++)
			{
				res.Add(CreateFloatList(lst[i].val as List<Item>));
			}
			return res;
		}
#if DEBUG
		static Face dbgfc = null;
#endif

		private object CreateEntity(Item item)
		{
			if (item == null) return null;
			int defind = -1;
			if (item.type == Item.ItemType.index)
			{
				defind = item.definingIndex = (int)item.val;
			}
			if (item.type == Item.ItemType.index) item = definitions[(int)item.val]; // resolve reference
#if DEBUG
			if (17 == item.definingIndex)
			{

			}
#endif
			if (!(item.val is List<Item>)) return item.val; // already created, maybe null
			if (defind >= 0) item.definingIndex = defind;
			lock (item) // no problem is not parallel
			{
#if DEBUG
				lock (definitionStack) definitionStack.Push(item.definingIndex);
#endif
				if (!(item.val is List<Item>)) return item.val; // already created, maybe null
				if (item.type == Item.ItemType.advancedFace || item.type == Item.ItemType.faceSurface || item.type == Item.ItemType.curveBoundedSurface) CreatingFace();
				switch (item.type)
				{
					case Item.ItemType.mechanicalDesignGeometricPresentationRepresentation: // name, items, context
						{
							GetContext(item.parameter["context_of_items"]);
							List<Item> lst = item.SubList(1);
							GeoObjectList gl = new GeoObjectList();
							if (Settings.GlobalSettings.GetBoolValue("StepImport.Parallel", true))
							{
								Parallel.For(0, lst.Count, i =>
								{
									object o = CreateEntity(lst[i]);
									lock (gl)
									{
										if (o is IGeoObject) gl.Add(o as IGeoObject);
										else if (o is IGeoObject[]) gl.AddRange(o as IGeoObject[]);
									}
								});
							}
							else
							{
								for (int i = 0; i < lst.Count; i++)
								{
									object o = CreateEntity(lst[i]);
									if (o is IGeoObject) gl.Add(o as IGeoObject);
									else if (o is IGeoObject[]) gl.AddRange(o as IGeoObject[]);
								}
							}
							item.val = gl;
						}
						break;
					case Item.ItemType.shapeDefinitionRepresentation: //  definition, used_representation
						{
							// "shapeDefinitionRepresentation(239646)->productDefinitionShape(239662)->productDefinition(239716)->productDefinitionFormation(239732)->product(239767)"
							// parameter: definition, definition, formation, of_product
							// parameter: definition, definition, id
							// parameter: used_representation, items, axis2d
							// this is typically a root object, we associate the product-item with the used_representation-itmes
							Item prodDef = item.parameter["definition"].parameter["definition"];
							if (prodDef.type == Item.ItemType.shapeAspect)
							{
								prodDef = prodDef.parameter["of_shape"].parameter["definition"];
							}
							string id = prodDef.parameter["id"].sval;
							CreateEntity(item.parameter["used_representation"]);
							List<Item> usedrep = item.parameter["used_representation"].parameter["items"].lval;
							Item context = item.parameter["used_representation"].parameter["context_of_items"];
							Item product = prodDef.parameter["formation"].parameter["of_product"];
							Item assoc, _context;
							lock (product)
							{
								if (!product.parameter.TryGetValue("_association", out assoc))
								{
									product.parameter["_association"] = assoc = new Item(Item.ItemType.created, new List<Item>());
									product.parameter["_context"] = _context = new Item(Item.ItemType.created, new List<Item>());
								}
								else
								{
									_context = product.parameter["_context"];
								}
								assoc.lval.AddRange(usedrep);
								_context.lval.Add(context);
							}
							lock (products)
							{
								products.Add(product);
							}
						}
						break;
					case Item.ItemType.nextAssemblyUsageOccurrence:
						{
							List<Item> lst = item.val as List<Item>;
							List<object> lobj = new List<object>();
							if (lst != null)
							{
								for (int i = 0; i < lst.Count; i++)
								{
									lobj.Add(CreateEntity(lst[i]));
								}
							}
							item.val = lobj;
						}
						break;
					case Item.ItemType.productDefinitionShape: // name, description, definition
						break;
					case Item.ItemType.productDefinition: //id, description, formation, frame_of_reference
						break;
					case Item.ItemType.product: // id, name, description, frame_of_reference
						break;
					case Item.ItemType.productDefinitionFormationWithSpecifiedSource: // id, description, of_product, make_or_buy
						break;
					case Item.ItemType.geometricCurveSet:
					case Item.ItemType.geometricSet: // elements
						{
							Item elements = item.parameter["elements"];
							GeoObjectList val = new GeoObjectList();
							for (int i = 0; i < elements.lval.Count; i++)
							{
								Item subItem = elements.lval[i];
								object o = CreateEntity(subItem);
								if (o is GeoPoint)
								{
									GeoObject.Point pnt = GeoObject.Point.Construct();
									pnt.Location = (GeoPoint)o;
									pnt.Symbol = PointSymbol.Cross;
									pnt.Name = subItem.parameter["name"].sval;
									val.Add(pnt);
									subItem.val = pnt; //For this CartesianPoint a Cadability Point is generated. I set it to the item instead of GeoPoint to make it work layer assignment.
								}
								else if (o is IGeoObject) val.Add(o as IGeoObject);
								else if (o is GeoObjectList) val.AddRange(o as GeoObjectList);
								else if (o is IGeoObject[]) val.AddRange(o as IGeoObject[]);
								else if (o is NurbsSurface nurbsSurface)
								{
									BoundingRect ext;
									nurbsSurface.GetNaturalBounds(out ext.Left, out ext.Right, out ext.Bottom, out ext.Top);
									Face face = Face.MakeFace(nurbsSurface, ext);
									if (face != null)
									{
#if DEBUG
										face.UserData["StepImport.ItemNumber"] = new UserInterface.IntegerProperty(elements.lval[i].definingIndex, "StepImport.ItemNumber");
#endif
										val.Add(face);
									}
								}
								else { }
							}
							item.val = val;
						}
						break;
					case Item.ItemType.manifoldSurfaceShapeRepresentation:
					case Item.ItemType.advancedBrepShapeRepresentation: // name, items, context_of_items
						{
							GetContext(item.parameter["context_of_items"]);
							string name = item.parameter["name"].val as string;
							List<Item> lst = item.parameter["items"].lval;
							GeoObjectList val = new GeoObjectList();
							for (int i = 0; i < lst.Count; i++)
							{
								object o = CreateEntity(lst[i]);
								if (!string.IsNullOrEmpty(name))
								{
									if (o is Shell && string.IsNullOrEmpty((o as Shell).Name)) (o as Shell).Name = name;
									if (o is Solid && string.IsNullOrEmpty((o as Solid).Name)) (o as Solid).Name = name;
									if (o is IGeoObject go) namedGeoObjects[name] = go;
								}
								if (o is IGeoObject) val.Add(o as IGeoObject);
								if (o is GeoObjectList) val.AddRange(o as GeoObjectList);
							}
							item.val = val;
						}
						break;
					case Item.ItemType.shellBasedSurfaceModel: // name, sbsm_boundary
						{
							string nm = item.SubString(0);
							List<Item> sublist = item.SubList(1);
							GeoObjectList val = new GeoObjectList();
							for (int i = 0; i < sublist.Count; i++)
							{
								object o = CreateEntity(sublist[i]);
								if (o is IGeoObject) val.Add(o as IGeoObject);
							}
							if (context != null && context.factor != 1.0 && context.factor > 0.0)
							{
								foreach (IGeoObject go in val)
								{
									go.Modify(ModOp.Scale(context.factor));
								}
							}
							item.val = val;
						}
						break;
					case Item.ItemType.overRidingStyledItem: // name, styles, item, over_ridden_style
						{
							ColorDef cd = null;
							string nm = item.SubString(0);
							List<Item> sublist = item.SubList(1);
							for (int i = 0; i < sublist.Count; i++)
							{
								object o = CreateEntity(sublist[i]);
								if (o is ColorDef) cd = o as ColorDef;
							}
							object ovr = CreateEntity(item.SubItem(3));
							item.val = CreateEntity(item.SubItem(2)); // TODO: clone(?) and use the name and styles
							if (ovr is ColorDef) cd = ovr as ColorDef;
							if (item.val is IColorDef && cd != null) (item.val as IColorDef).ColorDef = cd;
							if (item.val is IGeoObject[] goa)
							{
								for (int i = 0; i < goa.Length; i++)
								{
									if (goa[i] is IColorDef icd) icd.ColorDef = cd;
								}
							}
							if (item.val is Shell) (item.val as Shell).Name = nm;
						}
						break;
					case Item.ItemType.styledItem: // name, styles, item
						{
							ColorDef cd = null;
							string nm = item.SubString(0);
							List<Item> sublist = item.parameter["styles"].val as List<Item>;
							for (int i = 0; i < sublist.Count; i++)
							{
								object o = CreateEntity(sublist[i]);
								if (o is ColorDef) cd = o as ColorDef;
								// there could also be line styles
							}
							item.val = CreateEntity(item.SubItem(2));
							if (item.val is IColorDef && cd != null) (item.val as IColorDef).SetTopLevel(cd, true);
							if (item.val is IColorDef[] && cd != null)
							{
								for (int i = 0; i < (item.val as IColorDef[]).Length; i++)
								{
									(item.val as IColorDef[])[i].SetTopLevel(cd, true);
								}
							}
							if (item.val is GeoObjectList glst && cd != null)
							{
								for (int i = 0; i < glst.Count; i++)
								{
									if (glst[i] is IColorDef cdi) cdi.SetTopLevel(cd, true);
								}
							}
							if (!string.IsNullOrWhiteSpace(nm))
							{
								if (item.val is Shell) (item.val as Shell).Name = nm;
								if (item.val is Solid) (item.val as Solid).Name = nm;
							}
						}
						break;

					case Item.ItemType.draughtingPreDefinedColour:
						{
							string nm = item.SubString(0).ToLower();
							Color clr;
							switch (nm)
							{
								case "red":
									clr = Color.Red;
									break;
								default:
								case "green":
									clr = Color.Lime;
									break;
								case "blue":
									clr = Color.Blue;
									break;
								case "yellow":
									clr = Color.Yellow;
									break;
								case "magenta":
									clr = Color.Magenta;
									break;
								case "cyan":
									clr = Color.Cyan;
									break;
								case "black":
									clr = Color.Black;
									break;
								case "white":
									clr = Color.White;
									break;
							}
							ColorDef cd = new ColorDef(nm, clr);
							AvoidDuplicateColorNames(cd);
							item.val = cd;
						}
						break;
					case Item.ItemType.curveStyle: // curve_font, curve_width, curve_colour
						{   // curve_font is the line pattern

						}
						break;
					case Item.ItemType.manifoldSolidBrep: // name, outer
						{
							if (!(item.parameter["outer"].val is IGeoObject) && !(item.parameter["outer"].val is GeoObjectList))
							{
								object created = CreateEntity(item.parameter["outer"]);
								if (context != null && context.factor != 1.0 && context.factor > 0.0)
								{
									if (created is IGeoObject)
									{
										(created as IGeoObject).Modify(ModOp.Scale(context.factor));
									}
									if (created is GeoObjectList)
									{
										foreach (IGeoObject go in (created as GeoObjectList))
										{
											go.Modify(ModOp.Scale(context.factor));
										}
									}
								}
							}
							if (item.parameter["outer"].val is Solid sld) sld.Name = item.parameter["name"].sval;
							if (item.parameter["outer"].val is Shell shl) shl.Name = item.parameter["name"].sval;
							item.val = item.parameter["outer"].val;
						}
						break;
					case Item.ItemType.brepWithVoids:
						{   // a solid with other shells as holes. We currently only return the solid
							object shell = CreateEntity(item.parameter["outer"]);
							List<Item> voids = item.parameter["voids"].val as List<Item>;
							if (shell is GeoObjectList || shell is IGeoObject) item.val = shell;
							else
							{
								for (int i = 0; i < voids.Count; i++)
								{
									shell = CreateEntity(voids[i]);
									if (shell is GeoObjectList || shell is IGeoObject)
									{
										item.val = shell;
										break;
									}
								}
							}
						}
						break;
					case Item.ItemType.orientedClosedShell:
					case Item.ItemType.closedShell: // name, faces
						{
							List<Item> lst = item.SubList(1);
							if (lst == null && item.parameter.ContainsKey("closed_shell_element"))
							{
								item.val = CreateEntity(item.parameter["closed_shell_element"]);
								break;
							}
							List<Face> faces = new List<Face>();
							if (Settings.GlobalSettings.GetBoolValue("StepImport.Parallel", true))
							{
								Parallel.For(0, lst.Count, i =>
								{
									object o = CreateEntity(lst[i]);
									lock (faces)
									{
										if (o is Face) faces.Add(o as Face);
										if (o is Face[]) faces.AddRange(o as Face[]);
									}
								});
							}
							else
							{
								for (int i = 0; i < lst.Count; i++)
								{
									object o = CreateEntity(lst[i]);
									if (o is Face) faces.Add(o as Face);
									if (o is Face[]) faces.AddRange(o as Face[]);
								}
							}
							if (faces.Count == 0)
							{
								item.val = null; // nothing to do
								break;
							}
							Shell.connectFaces(faces.ToArray(), Precision.eps);
							Shell shell = Shell.MakeShell(faces.ToArray());
							shell.Name = item.SubString(0);
							if (Settings.GlobalSettings.GetBoolValue("StepImport.CombineFaces", true)) shell.CombineConnectedFaces();
#if DEBUG
							shell.UserData["StepImport.ItemNumber"] = new UserInterface.IntegerProperty(item.definingIndex, "StepImport.ItemNumber");
#endif
							Edge[] openedges = shell.OpenEdges;
							if (openedges.Length > 0)
							{
#if DEBUG
								GeoObjectList openFaces = new GeoObjectList();
								for (int i = 0; i < openedges.Length; i++)
								{
									if (openedges[i].Curve3D != null)
									{
										openFaces.Add(openedges[i].PrimaryFace);
									}
								}
#endif
								Dictionary<Edge, ISurface> edgesToRepair = new Dictionary<Edge, ISurface>();
								for (int i = 0; i < lst.Count; i++)
								{   // all the faces
									if (lst[i].val is List<List<StepEdgeDescriptor>> llsed) // this face has not been imported
									{
										foreach (List<StepEdgeDescriptor> lsed in llsed)
										{
											foreach (StepEdgeDescriptor sed in lsed)
											{
												if (sed.createdEdges != null)
												{
													for (int k = 0; k < openedges.Length; k++)
													{
														for (int l = 0; l < sed.createdEdges.Count; l++)
														{
															if (openedges[k] == sed.createdEdges[l])
															{
																if (lst[i].parameter.TryGetValue("face_geometry", out Item surface))
																	edgesToRepair[openedges[k]] = surface as ISurface;
															}
														}
													}
												}
											}
										}
									}
								}
								try
								{
									shell.CloseOpenEdges(edgesToRepair);
									if (shell.OpenEdgesExceptPoles.Length > 0)
									{
										bool reduced = false;
										do
										{
											int oe = shell.OpenEdgesExceptPoles.Length;
											shell.CloseOpenEdges(); // not sure why we sometimes need multiple calls
											reduced = shell.OpenEdgesExceptPoles.Length < oe;
										} while (reduced);
									}
								}
								catch { }
							}

							if (shell.OpenEdgesExceptPoles.Length == 0)
							{
								Solid sld = Solid.Construct();
								sld.SetShell(shell);
								if (!string.IsNullOrEmpty(shell.Name)) sld.Name = shell.Name;
								item.val = sld;
#if DEBUG
								sld.UserData["StepImport.ItemNumber"] = new UserInterface.IntegerProperty(item.definingIndex, "StepImport.ItemNumber");
#endif
							}
							else
							{
								// Edge[] openEdges = shell.OpenEdgesExceptPoles;
								item.val = shell;
								importProblems[item.definingIndex] = "closedShell with open edges";
#if DEBUG
								System.Diagnostics.Trace.WriteLine("closedShell with open edges: " + item.definingIndex.ToString());
#endif
							}
						}
						break;
					case Item.ItemType.openShell: // name, faces
						{
							List<Item> lst = item.SubList(1);
							List<Face> faces = new List<Face>();
							if (Settings.GlobalSettings.GetBoolValue("StepImport.Parallel", true))
							{
								Parallel.For(0, lst.Count, i =>
								{
									object o = CreateEntity(lst[i]);
									lock (faces)
									{
										if (o is Face) faces.Add(o as Face);
										if (o is Face[]) faces.AddRange(o as Face[]);
									}
								});
							}
							else
							{
								for (int i = 0; i < lst.Count; i++)
								{
									object o = CreateEntity(lst[i]);
									if (o is Face) faces.Add(o as Face);
									if (o is Face[]) faces.AddRange(o as Face[]);
								}
							}
							Shell.connectFaces(faces.ToArray(), Precision.eps);
							Shell shell = Shell.MakeShell(faces.ToArray());
							shell.Name = item.SubString(0);
							if (Settings.GlobalSettings.GetBoolValue("StepImport.CombineFaces", false)) shell.CombineConnectedFaces();
							item.val = shell;
						}
						break;
					case Item.ItemType.curveBoundedSurface: // basis_surface   : Surface; boundaries: SET[1 : ?] OF Boundary_Curve; implicit_outer: BOOLEAN;
						{
#if DEBUG
							if (237 == item.definingIndex || 13754 == item.definingIndex)
							{
							}
#endif
							List<List<StepEdgeDescriptor>> bounds = new List<List<StepEdgeDescriptor>>();
							foreach (Item sub in item.parameter["boundaries"].lval)
							{
								List<StepEdgeDescriptor> le = CreateEntity(sub) as List<StepEdgeDescriptor>;
								if (le != null) bounds.Add(le);
							}
							ISurface surface = CreateEntity(item.parameter["basis_surface"]) as ISurface;
							try
							{
								double precision = Precision.eps;
								if (context != null && context.uncertainty > 0.0) precision = context.uncertainty;
								if (surface != null && bounds.Count > 0)
								{
									item.val = Face.MakeFacesFromStepAdvancedFace(surface, bounds, item.parameter["implicit_outer"].bval, precision);
								}
								else item.val = null;
								if (item.val is Face[] && (item.val as Face[]).Length > 0)
								{
									for (int i = 0; i < (item.val as Face[]).Length; i++)
									{
										(item.val as Face[])[i].Name = item.SubString(0);
										(item.val as Face[])[i].UserData["StepImport.ItemNumber"] = new UserInterface.IntegerProperty(item.definingIndex, "StepImport.ItemNumber");
#if DEBUG
										if (faceCount % 1000 == 0)
										{
											//System.Diagnostics.Trace.WriteLine("hashCount, memory: " + (item.val as Face[])[i].GetHashCode().ToString() + ", " + System.GC.GetTotalMemory(true).ToString());
										}
										if (!(item.val as Face[])[i].CheckConsistency())
										{
											System.Diagnostics.Trace.WriteLine("invalid Face: " + item.definingIndex.ToString());
											importProblems[item.definingIndex] = "inconsistent face";
										}
										if (93129 == item.definingIndex)
										{
											(item.val as Face[])[i].AssureTriangles(0.4);
										}
										++faceCount;
#else
                                        if (!(item.val as Face[])[i].CheckConsistency())
                                        {
                                            importProblems[item.definingIndex] = "inconsistent face";
                                        }
#endif
									}
								}
								else
								{
#if DEBUG
									System.Diagnostics.Trace.WriteLine("Face not imported: " + item.definingIndex.ToString());
#endif
									importProblems[item.definingIndex] = "face not imported";
									item.val = bounds; // save the bounds, maybe we can reconstruct the face later
								}
							}
							catch (Exception ex)
							{
								item.val = null;
#if DEBUG
								System.Diagnostics.Trace.WriteLine("Exception on Face import: " + item.definingIndex.ToString());
#endif
								if (ex == null) importProblems[item.definingIndex] = "exception on face import: null";
								else importProblems[item.definingIndex] = "exception on face import: " + ex.Message;
								item.val = bounds; // save the bounds, maybe we can reconstruct the face later
							}
						}
						break;
					case Item.ItemType.faceSurface:
					case Item.ItemType.advancedFace: // name, bounds, face_geometry, same_sense
						{
#if DEBUG
							if (237 == item.definingIndex || 205 == item.definingIndex)
							{
							}
#endif
							List<List<StepEdgeDescriptor>> bounds = new List<List<StepEdgeDescriptor>>();
							foreach (Item sub in item.parameter["bounds"].lval)
							{
								List<StepEdgeDescriptor> le = CreateEntity(sub) as List<StepEdgeDescriptor>;
								if (le != null) bounds.Add(le);
							}
							ISurface surface = CreateEntity(item.parameter["face_geometry"]) as ISurface;
							try
							{
								double precision = Precision.eps;
								if (context != null && context.uncertainty > 0.0 && context.uncertainty < precision) precision = context.uncertainty;
								if (surface != null && bounds.Count > 0)
								{
									System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
									stopWatch.Start();
									// we need to clone th surface here, because the same surface might be used by two independent faces
									// and MakeFacesFromStepAdvancedFace may modify the surface (e.g. rotate a cylinder around its axis to better fit the 2d curves)
									item.val = Face.MakeFacesFromStepAdvancedFace(surface.Clone(), bounds, item.parameter["same_sense"].bval, precision);
									stopWatch.Stop();
									if (stopWatch.ElapsedMilliseconds > elapsedMs)
									{
										elapsedMs = stopWatch.ElapsedMilliseconds;
										defIndex = item.definingIndex;
										System.Diagnostics.Trace.WriteLine("Face: " + item.definingIndex.ToString() + ", time: " + elapsedMs.ToString());
									}
#if DEBUG
									if (4890 == item.definingIndex)
									{
										dbgfc = (item.val as Face[])[0];
										//GeoPoint2D dbg2d = dbgfc.Surface.PositionOf(dbgfc.Surface.PointAt(new GeoPoint2D(1.5, 0.6)));
										//dbgfc.AssureTriangles(0.02);
									}
									if (dbgfc != null)
									{
										if (!dbgfc.CheckConsistency()) { }
									}
#endif

								}
								else item.val = null;
								if (item.val is Face[] && (item.val as Face[]).Length > 0)
								{
									for (int i = 0; i < (item.val as Face[]).Length; i++)
									{
										Face fc = (item.val as Face[])[i];
										fc.Name = item.SubString(0);
										(item.val as Face[])[i].UserData["StepImport.ItemNumber"] = new UserInterface.IntegerProperty(item.definingIndex, "StepImport.ItemNumber");
#if DEBUG
										if (faceCount % 1000 == 0)
										{
											//System.Diagnostics.Trace.WriteLine("hashCount, memory: " + (item.val as Face[])[i].GetHashCode().ToString() + ", " + System.GC.GetTotalMemory(true).ToString());
										}
										if (!(item.val as Face[])[i].CheckConsistency())
										{
											System.Diagnostics.Trace.WriteLine("invalid Face: " + item.definingIndex.ToString());
											importProblems[item.definingIndex] = "inconsistent face";
										}
										++faceCount;
										//if (2859 == item.definingIndex || 3219 == item.definingIndex)
										//    (item.val as Face[])[i].AssureTriangles(0.004); // remove when done

										//foreach (Edge edg in (item.val as Face[])[i].Edges)
										//{
										//    if (edg.PrimaryFace != (item.val as Face[])[i]) edg.PrimaryFace.CheckConsistency();
										//    if (edg.SecondaryFace != null && edg.SecondaryFace != (item.val as Face[])[i]) edg.SecondaryFace.CheckConsistency();
										//}
#else
                                        if (!(item.val as Face[])[i].CheckConsistency())
                                        {
                                            importProblems[item.definingIndex] = "inconsistent face";
                                        }
#endif
									}
								}
								else
								{
#if DEBUG
									System.Diagnostics.Trace.WriteLine("Face not imported: " + item.definingIndex.ToString());
#endif
									importProblems[item.definingIndex] = "face not imported";
									notImportedFaces.Add(item);
									item.val = bounds; // save the bounds, maybe we can reconstruct the face later
								}
							}
							catch (Exception ex)

							{
								item.val = null;
								item.val = bounds; // save the bounds, maybe we can reconstruct the face later
#if DEBUG


								System.Diagnostics.Trace.WriteLine("Exception on Face import: " + item.definingIndex.ToString());
#endif
								if (ex == null) importProblems[item.definingIndex] = "exception on face import: null";
								else
									importProblems[item.definingIndex] = "exception on face import: " + ex.Message;
								notImportedFaces.Add(item);
							}
						}
						break;
					case Item.ItemType.cartesianPoint: // name, coordinates
						{
							List<Item> lst = item.SubList(1);
							if (lst.Count == 2) item.val = new GeoPoint2D((double)lst[0].val, (double)lst[1].val);
							else if (lst.Count == 3) item.val = new GeoPoint((double)lst[0].val, (double)lst[1].val, (double)lst[2].val);
							else item.val = null;
						}
						break;
					case Item.ItemType.direction: // name, coordinates
						{
							List<Item> lst = item.SubList(1);
							if (lst.Count == 2) item.val = new GeoVector2D((double)lst[0].val, (double)lst[1].val);
							else if (lst.Count == 3) item.val = new GeoVector((double)lst[0].val, (double)lst[1].val, (double)lst[2].val);
							else item.val = null;
						}
						break;
					case Item.ItemType.axis2Placement_2d: // name, location, ref_direction
						{
							object loc = CreateEntity(item.parameter["location"]);
							object axis = CreateEntity(item.parameter["ref_direction"]);
							if (loc is GeoPoint2D cnt2d && axis is GeoVector2D dir2d)
							{
								item.val = new Pair<GeoPoint2D, GeoVector2D>(cnt2d, dir2d);
							}
							else
							{
								item.val = null; // not sure, whether axis2Placement_2d may also have 3d components, no case found yet
							}
						}
						break;
					case Item.ItemType.axis2Placement_3d: // name, location, axis, ref_direction
						{
							object loc = CreateEntity(item.SubItem(1));
							object axis = CreateEntity(item.SubItem(2));
							object dirx = CreateEntity(item.SubItem(3));
							if (loc is GeoPoint && axis is GeoVector && dirx is GeoVector)
							{
								GeoVector diry = (GeoVector)axis ^ (GeoVector)dirx;
								item.val = new FreeCoordSys((GeoPoint)loc, (GeoVector)dirx, diry, (GeoVector)axis);
							}
							else if (loc is GeoPoint && axis is GeoVector)
							{
								GeoVector vaxis = (GeoVector)axis;
								if (Math.Abs(vaxis.x) < Math.Abs(vaxis.y))
								{
									if (Math.Abs(vaxis.x) < Math.Abs(vaxis.z)) dirx = vaxis ^ GeoVector.XAxis;
									else dirx = vaxis ^ GeoVector.ZAxis;
								}
								else
								{
									if (Math.Abs(vaxis.y) < Math.Abs(vaxis.z)) dirx = vaxis ^ GeoVector.YAxis;
									else dirx = vaxis ^ GeoVector.ZAxis;
								}
								GeoVector diry = (GeoVector)axis ^ (GeoVector)dirx;
								item.val = new FreeCoordSys((GeoPoint)loc, (GeoVector)dirx, diry, (GeoVector)axis);
							}
							else item.val = null;
						}
						break;
					case Item.ItemType.axis1Placement:
						{
							object loc = CreateEntity(item.SubItem(1));
							object axis = CreateEntity(item.SubItem(2));
							if (loc is GeoPoint && axis is GeoVector)
							{
								GeoVector dirx;
								GeoVector vaxis = (GeoVector)axis;
								if (Math.Abs(vaxis.x) < Math.Abs(vaxis.y))
								{
									if (Math.Abs(vaxis.x) < Math.Abs(vaxis.z)) dirx = vaxis ^ GeoVector.XAxis;
									else dirx = vaxis ^ GeoVector.ZAxis;
								}
								else
								{
									if (Math.Abs(vaxis.y) < Math.Abs(vaxis.z)) dirx = vaxis ^ GeoVector.YAxis;
									else dirx = vaxis ^ GeoVector.ZAxis;
								}
								GeoVector diry = (GeoVector)axis ^ (GeoVector)dirx;
								item.val = new FreeCoordSys((GeoPoint)loc, (GeoVector)dirx, diry, (GeoVector)axis);
							}
							else item.val = null;
						}
						break;
					case Item.ItemType.vector: // name, orientation, magnitude
						{
							object dir = CreateEntity(item.SubItem(1));
							if (dir is GeoVector) item.val = item.SubFloat(2) * (GeoVector)dir;
							else if (dir is GeoVector2D) item.val = item.SubFloat(2) * (GeoVector2D)dir;
							else item.val = null;
						}
						break;
					case Item.ItemType.compositeCurve: // segments, self_intersect 
						{
							List<Item> list = item.parameter["segments"].lval;
							GeoObjectList crvs = new GeoObjectList();
							for (int i = 0; i < list.Count; i++)
							{
								object o = CreateEntity(list[i]);
								if (o is ICurve) crvs.Add(o as IGeoObject);
							}
							item.val = crvs;
						}
						break;
					case Item.ItemType.compositeCurveSegment: // transition: Transition_Code; same_sense: BOOLEAN; parent_curve: Curve
						{
							object o = CreateEntity(item.parameter["parent_curve"]);
							if (o is ICurve)
							{
								bool ss = item.parameter["same_sense"].bval;
								if (!ss) (o as ICurve).Reverse();
								item.val = o;
							}
							else
							{
								item.val = null;
							}
						}
						break;
					case Item.ItemType.line: // name, pnt, dir
						{
							object pnt = CreateEntity(item.SubItem(1));
							object dir = CreateEntity(item.SubItem(2));
							if (pnt is GeoPoint && dir is GeoVector) item.val = Line.MakeLine((GeoPoint)pnt, (GeoPoint)pnt + (GeoVector)dir);
							else if (pnt is GeoPoint2D && dir is GeoVector2D) item.val = new Line2D((GeoPoint2D)pnt, (GeoPoint2D)pnt + (GeoVector2D)dir);
							else item.val = null;
						}
						break;
					case Item.ItemType.plane: // name, position
						{
							object fcs = CreateEntity(item.SubItem(1));
							if (fcs is FreeCoordSys) item.val = new PlaneSurface(((FreeCoordSys)fcs).Location, ((FreeCoordSys)fcs).DirectionX, ((FreeCoordSys)fcs).DirectionY);
							else item.val = null;
						}
						break;
					case Item.ItemType.cylindricalSurface: // name, position, radius
						{
							object o = CreateEntity(item.SubItem(1));
							if (o is FreeCoordSys)
							{
								FreeCoordSys fcs = (FreeCoordSys)o;
								double radius = item.SubFloat(2);
								CylindricalSurface cs = new CylindricalSurface(fcs.Location, radius * fcs.DirectionX.Normalized, radius * fcs.DirectionY.Normalized, fcs.DirectionZ);
								item.val = cs;
							}
							else
							{
								item.val = null;
							}
						}
						break;
					case Item.ItemType.sphericalSurface: // name, position, radius
						{
							object o = CreateEntity(item.SubItem(1));
							if (o is FreeCoordSys)
							{
								FreeCoordSys fcs = (FreeCoordSys)o;
								double radius = item.SubFloat(2);
								SphericalSurface ss = new SphericalSurface(fcs.Location, radius * fcs.DirectionX.Normalized, radius * fcs.DirectionY.Normalized, radius * fcs.DirectionZ.Normalized);
								item.val = ss;
							}
							else
							{
								item.val = null;
							}
						}
						break;
					case Item.ItemType.surfaceOfLinearExtrusion: // name, swept_curve, extrusion_axis
						{
							SurfaceOfLinearExtrusion srf = null;
							object o = CreateEntity(item.SubItem(1));
							if (o is ICurve)
							{
								ICurve crv = o as ICurve;
								o = CreateEntity(item.SubItem(2));
								if (o is GeoVector)
								{
									srf = new SurfaceOfLinearExtrusion(crv, (GeoVector)o, 0.0, 1.0);
#if DEBUG
									GeoObjectList dbg = srf.DebugGrid;
#endif
								}
							}
							item.val = srf;
						}
						break;
					case Item.ItemType.surfaceOfRevolution:
						{
							ISurface srf = null;
							object o = CreateEntity(item.SubItem(1));
							if (o is ICurve)
							{
								ICurve crv = o as ICurve;
								o = CreateEntity(item.SubItem(2));
								if (o is FreeCoordSys)
								{
									if (crv is Line && Precision.SameDirection(((FreeCoordSys)o).DirectionZ, crv.StartDirection, false))
									{
										GeoPoint loc = ((FreeCoordSys)o).Location;
										GeoVector dir = ((FreeCoordSys)o).DirectionZ;
										double d = Geometry.DistPL(crv.StartPoint, loc, dir);
										GeoVector dirx = ((FreeCoordSys)o).DirectionX.Normalized;
										GeoVector diry = (dir ^ dirx).Normalized;
										srf = new CylindricalSurface(loc, d * dirx, d * diry, dir); // Ortientation???
										if (((FreeCoordSys)o).DirectionZ * crv.StartDirection < 0)
										{
											srf.ReverseOrientation();
										}
									}
									else if (crv is Ellipse && (crv as Ellipse).IsCircle && Precision.IsPerpendicular((crv as Ellipse).Plane.Normal, ((FreeCoordSys)o).DirectionZ, false))
									{   // this is a torus
										FreeCoordSys fcs = (FreeCoordSys)o;
										Ellipse elli = crv as Ellipse;
										GeoPoint loc = Geometry.DropPL(elli.Center, fcs.Location, fcs.DirectionZ);
										double d = Geometry.DistPL(elli.Center, loc, fcs.DirectionZ);
										if (d > elli.Radius) // a torus with self intersection is making problems (e.g. "1-ZB-3265780-100-DS Einsatz Elektroden.stp")
										{
											srf = new ToroidalSurface(loc, fcs.DirectionX, fcs.DirectionY, fcs.DirectionZ, d, elli.Radius);
											GeoVector toCenter = elli.Center - loc;
											if (!elli.CounterClockWise) toCenter = -toCenter;
											if ((fcs.DirectionZ ^ elli.Plane.Normal) * toCenter > 0)
											{
												srf.ReverseOrientation();
											}
										}
									}
									if (srf == null)
									{
										srf = new SurfaceOfRevolution(crv, ((FreeCoordSys)o).Location, ((FreeCoordSys)o).DirectionZ);
									}
								}
							}
							item.val = srf;
						}
						break;
					case Item.ItemType.toroidalSurface: // name, position, major_radius,  minor_radius
						{
							ToroidalSurface srf = null;
							object o = CreateEntity(item.SubItem(1));
							if (o is FreeCoordSys)
							{
								FreeCoordSys fcs = (FreeCoordSys)o;
								double majorRadius = item.SubFloat(2);
								double minorRadius = item.SubFloat(3);
								srf = new ToroidalSurface(fcs.Location, fcs.DirectionX.Normalized, fcs.DirectionY.Normalized, fcs.DirectionZ, majorRadius, minorRadius);
							}
							item.val = srf;
						}
						break;
					case Item.ItemType.degenerateToroidalSurface: // name, position, major_radius,  minor_radius, select_outer 
						{
							ToroidalSurface srf = null;
							object o = CreateEntity(item.SubItem(1));
							if (o is FreeCoordSys)
							{
								FreeCoordSys fcs = (FreeCoordSys)o;
								double majorRadius = item.SubFloat(2);
								double minorRadius = item.SubFloat(3);
								srf = new ToroidalSurface(fcs.Location, fcs.DirectionX.Normalized, fcs.DirectionY.Normalized, fcs.DirectionZ, majorRadius, minorRadius);
							}
							item.val = srf;
						}
						break;
					case Item.ItemType.conicalSurface: // name, position, radius, semi_angle
						{
							ConicalSurface srf = null;
							object o = CreateEntity(item.SubItem(1));
							if (o is FreeCoordSys)
							{
								FreeCoordSys fcs = (FreeCoordSys)o;
								double radius = item.SubFloat(2);
								double semiAngle = item.SubFloat(3);
								if (context != null && context.toRadian != 0.0) semiAngle *= context.toRadian; // sometimes this is radian, sometimes degree
																											   // CONVERSION_BASED_UNIT('DEGREE',#6392) aus 1022_11988_1B_E.step mach Degree
								double d = radius / Math.Tan(semiAngle);
								// d*Math.Sin(semiAngle) ?
								srf = new ConicalSurface(fcs.Location - d * fcs.DirectionZ.Normalized, fcs.DirectionX.Normalized, fcs.DirectionY.Normalized, fcs.DirectionZ.Normalized, semiAngle, radius);
							}
							item.val = srf;
						}
						break;
					case Item.ItemType.offsetSurface: // name, basis_surface, distance, self_intersect
						{
							object o = CreateEntity(item.SubItem(1));
							if (o is ISurfaceImpl si)
							{
								item.val = si.GetOffsetSurface(item.SubFloat(2));
							}
							else if (o is ISurface)
							{
								double distance = item.SubFloat(2);
								OffsetSurface srf = new OffsetSurface(o as ISurface, distance);
								item.val = srf;
							}
							else
							{
								item.val = null;
							}
						}
						break;
					case Item.ItemType.quasiUniformSurface:
						{
							int uDegree = item["u_degree"].ival;
							int vDegree = item["v_degree"].ival;
							List<List<GeoPoint>> poles = CreateGeoPointList2(item["control_points_list"].lval);
							GeoPoint[,] polesa = new GeoPoint[poles.Count, poles[0].Count];
							for (int i = 0; i < polesa.GetLength(0); i++)
								for (int j = 0; j < polesa.GetLength(1); j++)
								{
									polesa[i, j] = poles[i][j];
								}
							BSplineSurfaceForm form = ParseEnum<BSplineSurfaceForm>(item.parameter["surface_form"].sval as string);
							bool uClosed = item["u_closed"].bval;
							bool vClosed = item["v_closed"].bval;
							NurbsSurface ns = new NurbsSurface(polesa, uDegree, vDegree, uClosed, vClosed);
							item.val = ns;
						}
						break;
					case Item.ItemType.rationalBSplineSurface:
					case Item.ItemType.bSplineSurfaceWithKnots:
					case Item.ItemType.bSplineSurface:
						{
							int uDegree = item.parameter["u_degree"].ival;
							int vDegree = item.parameter["v_degree"].ival;
							List<List<GeoPoint>> poles = CreateGeoPointList2(item.parameter["control_points_list"].lval);
							GeoPoint[,] polesa = new GeoPoint[poles.Count, poles[0].Count];
							for (int i = 0; i < polesa.GetLength(0); i++)
								for (int j = 0; j < polesa.GetLength(1); j++)
								{
									polesa[i, j] = poles[i][j];
								}
							BSplineSurfaceForm form = ParseEnum<BSplineSurfaceForm>(item.parameter["surface_form"].sval as string);
							bool uClosed = item.parameter["u_closed"].bval;
							bool vClosed = item.parameter["v_closed"].bval;
							bool selfIntersect = item.parameter["self_intersect"].bval;
							List<int> uMultiplicities;
							List<int> vMultiplicities;
							List<double> uKnots;
							List<double> vKnots;
							if (item.parameter.TryGetValue("u_multiplicities", out Item dumy))
							{
								uMultiplicities = CreateIntList(item.parameter["u_multiplicities"].lval);
								vMultiplicities = CreateIntList(item.parameter["v_multiplicities"].lval);
								uKnots = CreateFloatList(item.parameter["u_knots"].lval); // is there also a bspline surface without knots???
								vKnots = CreateFloatList(item.parameter["v_knots"].lval);
								Knot_Type knotSpec = ParseEnum<Knot_Type>(item.parameter["knot_spec"].sval);
#if DEBUG
								if (item.definingIndex == 7227) { }
#endif
							}
							else
							{   // no knots given
								// knots.Length - degree - 1 != poles.Length
								int uknotslength = polesa.GetLength(0) + uDegree + 1 - 2 * (uDegree + 1); // first and last multiplicity is degree+1
								int vknotslength = polesa.GetLength(1) + vDegree + 1 - 2 * (vDegree + 1);
								uKnots = new List<double>(2 + uknotslength);
								vKnots = new List<double>(2 + vknotslength);
								uMultiplicities = new List<int>(2 + uknotslength);
								vMultiplicities = new List<int>(2 + vknotslength);
								uKnots.Add(0.0);
								uMultiplicities.Add(uDegree + 1);
								for (int i = 0; i < uknotslength; i++)
								{
									uKnots.Add((double)i / (double)uknotslength);
									uMultiplicities.Add(1);
								}
								uKnots.Add(1.0);
								uMultiplicities.Add(uDegree + 1);
								vKnots.Add(0.0);
								vMultiplicities.Add(vDegree + 1);
								for (int i = 0; i < vknotslength; i++)
								{
									vKnots.Add((double)i / (double)vknotslength);
									vMultiplicities.Add(1);
								}
								vKnots.Add(1.0);
								vMultiplicities.Add(vDegree + 1);
							}
							double[,] weights = null;
							if (item.parameter.TryGetValue("weights_data", out Item itWeights))
							{
								List<List<double>> lweights = CreateFloatList2(itWeights.lval);
								weights = new double[lweights.Count, lweights[0].Count];
								for (int i = 0; i < weights.GetLength(0); i++)
									for (int j = 0; j < weights.GetLength(1); j++)
									{
										weights[i, j] = lweights[i][j];
									}
							}
							ISurface ns = MakeNurbsSurface(polesa, weights, uKnots.ToArray(), vKnots.ToArray(), uMultiplicities.ToArray(), vMultiplicities.ToArray(), uDegree, vDegree, uClosed, vClosed, form);
							item.val = ns;
						}

						break;
					case Item.ItemType.surface:
						{   // does not contain any information
							item.val = null;
						}
						break;
					case Item.ItemType.boundedSurface:
						{   // does not contain any information
							item.val = null;
						}
						break;
					case Item.ItemType.vertexPoint: // name, vertex_geometry
						{
							object pnt = CreateEntity(item.SubItem(1));
							if (pnt is GeoPoint) item.val = new Vertex((GeoPoint)pnt);
							else item.val = null;
						}
						break;
					case Item.ItemType.edgeCurve: // name, edge_start, Edge_end, edge_geometry, same_sense
						{
							Vertex v1 = CreateEntity(item.parameter["edge_start"]) as Vertex;
							Vertex v2 = CreateEntity(item.parameter["edge_end"]) as Vertex;
							bool sameSense = item.parameter["same_sense"].bval;
							// before creating the curve, we add the startpoint and endpoint of that curve to the item, so we can decide, whether to use the 3d or 2d representation
							if (sameSense)
							{
								item.parameter["edge_geometry"].parameter["_startpoint"] = item.parameter["edge_start"];
								item.parameter["edge_geometry"].parameter["_endpoint"] = item.parameter["edge_end"];
							}
							else
							{
								item.parameter["edge_geometry"].parameter["_startpoint"] = item.parameter["edge_end"];
								item.parameter["edge_geometry"].parameter["_endpoint"] = item.parameter["edge_start"];
							}
							ICurve crv = CreateEntity(item.parameter["edge_geometry"]) as ICurve;
							if (v1 != null && v2 != null && crv != null)
							{
								if (v1 != null && v1 == v2 && crv is BSpline && (crv.StartPoint | crv.EndPoint) > Precision.eps)
								{
									crv = (crv as BSpline).TrimOverlapping(v1.Position);
									if (!sameSense) crv.Reverse();
									crv.StartPoint = v1.Position;
									crv.EndPoint = v1.Position;
								}
								else
								{
									crv = crv.Clone();
									if (!sameSense) crv.Reverse();
									double sp = crv.PositionOf(v1.Position);
									double ep = crv.PositionOf(v2.Position);
									if (sp >= -1e-6 && sp <= 1 + 1e-6 && ep >= -1e-6 && ep <= 1 + 1e-6)
									{
										if (Math.Abs(sp - ep) < 1e-8) // changed to 1e-8, because there are valid ellipses with very small arcs out there (20015-001-002_ELE_ASM.stp)
										{
											// this is typically a full circle or ellipse
											if (sp != 0.0 && crv is Ellipse)
											{
												Ellipse elli = (crv as Ellipse);
												elli.StartParameter = elli.StartParameter + sp * elli.SweepParameter;
											}
											else if (crv is BSpline && (crv.StartPoint | crv.EndPoint) < Precision.eps)
											{
												if (sp > 1e-6 && sp < 1 - 1e-6)
												{
													crv = (crv as BSpline).SetCyclicalStartPoint(sp);
												}
											}
											else if (crv is Line)
											{
												if (sp < ep) crv.Trim(sp, ep);
												else
												{   // this should never happen, not sure whether first crv.Trim(sp, ep); and then reverse
													crv.Reverse();
													crv.Trim(ep, sp);
												}
											}
										}
										else if (sp < ep)
										{
											crv.Trim(sp, ep);
											if (crv is BSpline)
											{   // make it more precise: setting the first and last pole
												crv.StartPoint = v1.Position;
												crv.EndPoint = v2.Position;
											}
											else if (crv is Ellipse elli)
											{   // when the ellipse is trimmed there might be a big error, especially when the two radii differ very much
												// so here we need to correct the ellipse (as in 06_PN_4648_S_1185_1_I15_A13_AS_P100-1.stp)
												double error = (crv.StartPoint | v1.Position) + (crv.EndPoint | v2.Position);
												if (error > elli.Length * 1e-2)
												{
													elli.SetElliStartEndPoint(v1.Position, v2.Position);
												}
											}
										}
										else
										{
											if (crv is Ellipse)
											{   // take the part that crosses the x-axis (of the ellipse plane)
												crv.Trim(sp, ep);
											}
											else
											{
												if (Precision.IsEqual(crv.StartPoint, crv.EndPoint) && sp > 1 - 1e-6 || ep < 1e-6)
												{   // PositionOf at the end, when start- and endpoint are identical, is ambiguous
													if (sp > 1 - 1e-6 && ep < 1e-6)
													{
														// nothing to do: full curve
													}
													else if (sp > 1 - 1e-6)
													{
														crv.Trim(0.0, ep);
													}
													else if (ep < 1e-6)
													{
														crv.Trim(sp, 1.0);

													}
												}
												else if (Precision.IsEqual(crv.StartPoint, crv.EndPoint) && crv is BSpline)
												{   // like in "1252 EDS-EL-027.stp", item 2238
													// a closed BSpline, trimmed across the start/endpoint connection
													// Problems[item.definingIndex] = "Debug: trimming spline across closed bound";
													try
													{   // does not always work, e.g. file exp.stp
														crv = (crv as BSpline).SetCyclicalStartPoint(sp);
														ep = crv.PositionOf(v2.Position);
														crv.Trim(0.0, ep);
													}
													catch { }
												}
												else
												{
													crv.Reverse();
													crv.Trim(ep, sp);
												}
											}
										}
									}
									else
									{   // the problem here is: the start- and endpoint may exceed the finite curve crv. Probably a surface curve as a line, which is in step not limited
										if (Math.Abs(sp - ep) < 1e-6 && crv is Ellipse elli)
										{
											elli.StartPoint = v1.Position;
											if (elli.SweepParameter > 0) elli.SweepParameter = Math.PI * 2;
											else elli.SweepParameter = -Math.PI * 2;
										}
										else
										{
											crv.StartPoint = v1.Position; // we will need ICurve.SetStartEnd(GeoPoint sp, GeoPoint ep) because of arcs
											crv.EndPoint = v2.Position;
										}
									}
								}
#if DEBUG
							(crv as IGeoObject).UserData["Step.DefiningIndex"] = new UserInterface.IntegerProperty(item.SubItem(3).definingIndex, "StepImport.ItemNumber");
#endif
								item.val = new StepEdgeDescriptor(crv, v1, v2, true);
#if DEBUG
								(item.val as StepEdgeDescriptor).stepDefiningIndex = item.definingIndex;
#endif
							}
							else item.val = null;
						}
						break;
					case Item.ItemType.orientedEdge: // name, edge_start, edge_end, edge_element, orientation
						{
							Item edge_start = item.parameter["edge_start"];
							Item edge_end = item.parameter["edge_end"];
							StepEdgeDescriptor ec = CreateEntity(item.parameter["edge_element"]) as StepEdgeDescriptor;
							bool fwd = item.parameter["orientation"].bval;
							item.val = null;
							if (ec != null)
							{
								Vertex v1, v2;
								if (edge_start.type == Item.ItemType.use_default) v1 = ec.vertex1;
								else v1 = CreateEntity(edge_start) as Vertex;
								if (edge_end.type == Item.ItemType.use_default) v2 = ec.vertex2;
								else v2 = CreateEntity(edge_end) as Vertex;
#if DEBUG
								double pos1 = ec.curve.PositionOf(v1.Position);
								double pos2 = ec.curve.PositionOf(v2.Position);
								if (pos1 > pos2)
								{

								}
#endif
								if (v1 != null && v2 != null)
								{
									item.val = new StepEdgeDescriptor(ec, v1, v2, fwd); //  !(fwd ^ ec.forward)); // is the xor correct?
								}
							}
						}
						break;
					case Item.ItemType.vertexLoop: // name, vertex
						{
							// is this a pole??
							List<StepEdgeDescriptor> le = new List<StepEdgeDescriptor>();
							object o = CreateEntity(item.SubItem(1));
							if (o is Vertex)
							{
								le.Add(new StepEdgeDescriptor((ICurve)null, o as Vertex, o as Vertex, true));
							}
							item.val = le;
						}
						break;
					case Item.ItemType.edgeLoop: // name, list of oriented edges?
						{
							if (item.parameter.ContainsKey("edge_list"))
							{
								List<StepEdgeDescriptor> le = new List<StepEdgeDescriptor>();
								foreach (Item sub in item.parameter["edge_list"].lval)
								{
									object o = CreateEntity(sub);
									if (o is StepEdgeDescriptor)
									{
										StepEdgeDescriptor edg = o as StepEdgeDescriptor;
										le.Add(edg);
									}
									else if (o is ICurve)
									{
										StepEdgeDescriptor edg = new StepEdgeDescriptor(o as ICurve);
										le.Add(edg);
									}
								}
								item.val = le;
							}
							else
							{
								item.val = null;
							}
						}
						break;
					case Item.ItemType.faceBound:
					case Item.ItemType.faceOuterBound: // name, bound, orientation
						{
							item.val = CreateEntity(item.parameter["bound"]);
							if (!item.parameter["orientation"].bval && item.val is List<StepEdgeDescriptor> lse)
							{
								for (int i = 0; i < lse.Count; i++)
								{
									lse[i].forward = !lse[i].forward;
								}
								lse.Reverse(); // not sure we need to reverse here
							}
						}
						break;
					case Item.ItemType.list: // not in use any more?
						{   // this is some strange concept of step data:
							// types and subtypes are specified together in a list (not even comma separated)
							// We try to construct a complete type out of it and call CreateEntity with the newly constructed item.
							// Unfortunately several derived types are parallel to a super-type like in:
							//#6380=(
							//GEOMETRIC_REPRESENTATION_CONTEXT(3)
							//GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT((#6385))
							//GLOBAL_UNIT_ASSIGNED_CONTEXT((#6394,#6390,#6389))
							//REPRESENTATION_CONTEXT('1988_12_0.6_P_2_0', 'COMPONENT_PART')
							//);
							// where REPRESENTATION_CONTEXT is the base class (they call it super-type) of all three, so in theory, we would have to construct 3 different items
							Dictionary<Item.ItemType, object> val = new Dictionary<Item.ItemType, object>();
							SortedDictionary<int, Item> dict = new SortedDictionary<int, Item>();
							dict[0] = new Item(Item.ItemType.stringval, "");
							List<Item> lst = item.val as List<Item>;
							Item.ItemType restype = Item.ItemType.invalid;
							for (int i = 0; i < lst.Count; i++)
							{
								if (lst[i].type == Item.ItemType.index) lst[i] = definitions[(int)lst[i].val]; // resolve reference

								switch (lst[i].type)
								{
									case Item.ItemType.surface:
									case Item.ItemType.geometricRepresentationItem:
									case Item.ItemType.boundedSurface:
									case Item.ItemType.boundedCurve:
									case Item.ItemType.curve:
										break; // no data with this object
									case Item.ItemType.representationItem:
										dict[0] = (lst[i].val as List<Item>)[0]; // name
										break;
									case Item.ItemType.bSplineSurface:
										dict[1] = (lst[i].val as List<Item>)[0]; // uDegree
										dict[2] = (lst[i].val as List<Item>)[1]; // vDegree
										dict[3] = (lst[i].val as List<Item>)[2]; // poles
										dict[4] = (lst[i].val as List<Item>)[3]; // form
										dict[5] = (lst[i].val as List<Item>)[4]; // uClosed
										dict[6] = (lst[i].val as List<Item>)[5]; // vClosed
										dict[7] = (lst[i].val as List<Item>)[6]; // selfIntersect
										break;
									case Item.ItemType.bSplineSurfaceWithKnots:
										dict[8] = (lst[i].val as List<Item>)[0]; // u_multiplicities
										dict[9] = (lst[i].val as List<Item>)[1]; // v_multiplicities
										dict[10] = (lst[i].val as List<Item>)[2]; // u_knots
										dict[11] = (lst[i].val as List<Item>)[3]; // v_knots
										dict[12] = (lst[i].val as List<Item>)[4]; // spec
										if (restype == Item.ItemType.invalid) restype = Item.ItemType.bSplineSurfaceWithKnots;
										break;
									case Item.ItemType.rationalBSplineSurface:
										dict[13] = (lst[i].val as List<Item>)[0]; // weights
										restype = Item.ItemType.rationalBSplineSurface;
										break;
									case Item.ItemType.bSplineCurve:
										dict[1] = (lst[i].val as List<Item>)[0]; // degree
										dict[2] = (lst[i].val as List<Item>)[1]; // points
										dict[3] = (lst[i].val as List<Item>)[2]; // form
										dict[4] = (lst[i].val as List<Item>)[3]; // closed
										dict[5] = (lst[i].val as List<Item>)[4]; // selfIntersect
										break;
									case Item.ItemType.bSplineCurveWithKnots:
										dict[6] = (lst[i].val as List<Item>)[0]; // multiplicities
										dict[7] = (lst[i].val as List<Item>)[1]; // knots
										dict[8] = (lst[i].val as List<Item>)[2]; // spec
										if (restype == Item.ItemType.invalid) restype = Item.ItemType.bSplineCurveWithKnots;
										break;
									case Item.ItemType.rationalBSplineCurve:
										dict[9] = (lst[i].val as List<Item>)[0]; // weights
										restype = Item.ItemType.rationalBSplineCurve;
										break;
									case Item.ItemType.representationContext:
										dict[0] = (lst[i].val as List<Item>)[0]; // identifier
										dict[1] = (lst[i].val as List<Item>)[1]; // text
										break;
									case Item.ItemType.globalUnitAssignedContext:
										dict[2] = (lst[i].val as List<Item>)[0]; // units
										restype = Item.ItemType.globalUnitAssignedContext;
										break;
									case Item.ItemType.namedUnit:
										dict[0] = (lst[i].val as List<Item>)[0]; // dimensions?
										break;
									case Item.ItemType.conversionBasedUnit:
										dict[1] = (lst[i].val as List<Item>)[0]; // name
										dict[2] = (lst[i].val as List<Item>)[1]; // conversion_factor
										restype = Item.ItemType.conversionBasedUnit;
										break;
									case Item.ItemType.planeAngleUnit:
										break;
									case Item.ItemType.globalUncertaintyAssignedContext:
										dict[4] = (lst[i].val as List<Item>)[0]; // this is not exactely correct, but due to the strange step design
																				 // var dbg = CreateEntity((lst[i].val as List<Item>)[0]);
										break;
									case Item.ItemType.geometricRepresentationContext:
									case Item.ItemType.parametricRepresentationContext:
										break; // not interested in this data
									case Item.ItemType.representationRelationshipWithTransformation:
										dict[4] = (lst[i].val as List<Item>)[0];
										restype = lst[i].type;
										break;
									case Item.ItemType.representationRelationship:
										dict[0] = (lst[i].val as List<Item>)[0]; // name
										dict[1] = (lst[i].val as List<Item>)[1];
										dict[2] = (lst[i].val as List<Item>)[2];
										dict[3] = (lst[i].val as List<Item>)[3];
										break;
									default:
#if DEBUG
										//throw new ApplicationException("missing case in step import: " + lst[i].type.ToString());
#endif
										break;
								}
							}
							if (restype != Item.ItemType.invalid)
							{
								Item composed = new Item(restype, new List<Item>(dict.Values));
#if DEBUG
								composed.definingIndex = item.definingIndex;
#endif
								item.val = CreateEntity(composed);
							}
							else
							{
								item.val = null;
							}
						}
						break;
					case Item.ItemType.quasiUniformCurve:
					case Item.ItemType.bSplineCurveWithKnots: // name, degree, control_points_list, curve_form, closed_curve, self_intersect
					case Item.ItemType.rationalBSplineCurve: // name, degree, control_points_list, curve_form, closed_curve, self_intersect, weights
					case Item.ItemType.bSplineCurve: // name, degree, control_points_list, curve_form, closed_curve, self_intersect, weights
						{
							int degree = item.parameter["degree"].ival;
							object opoles = CreateGeoPointList(item.parameter["control_points_list"].lval);
							List<GeoPoint> poles = opoles as List<GeoPoint>;
							List<GeoPoint2D> poles2d = opoles as List<GeoPoint2D>;
							int count;
							if (poles != null) count = poles.Count;
							else count = poles2d.Count;
							BSplineCurveForm form = ParseEnum<BSplineCurveForm>(item.parameter["curve_form"].sval);
							bool closed = item.parameter["closed_curve"].bval;
							bool selfIntersect = item.parameter["self_intersect"].bval;
							List<int> multiplicities;
							List<double> knots;
							if (item.parameter.ContainsKey("knots"))
							{
								multiplicities = CreateIntList(item.parameter["knot_multiplicities"].lval);
								knots = CreateFloatList(item.parameter["knots"].lval);
								Knot_Type spec = ParseEnum<Knot_Type>(item.parameter["knot_spec"].sval);
							}
							else
							{
								//-	on a non-periodic curve, the number of poles is equal to the sum of the multiplicity coefficients, minus Degree, minus 1,
								multiplicities = new List<int>();
								multiplicities.Add(degree + 1);
								for (int i = 0; i < count - degree - 1; i++)
								{
									multiplicities.Add(1);
								}
								multiplicities.Add(degree + 1);
								knots = new List<double>();
								for (int i = 0; i < multiplicities.Count; i++)
								{
									knots.Add(i / (double)(multiplicities.Count - 1));
								}
							}
							double[] weights = null;
							if (item.parameter.TryGetValue("weights_data", out Item witem)) weights = CreateFloatList(witem.lval).ToArray();
							double plen = 0.0;
							if (poles != null)
							{
								for (int i = 1; i < poles.Count; i++)
								{
									plen += poles[i] | poles[i - 1];
								}
							}
							else
							{
								for (int i = 1; i < poles2d.Count; i++)
								{
									plen += poles2d[i] | poles2d[i - 1];
								}
							}
							double eps = Precision.eps;
							// in X08011002.stp we need the short splines
							if (context != null) eps = Math.Min(eps, context.uncertainty);
							if (plen < eps)
							{   // degenerated curve
								// Line line = Line.TwoPoints(poles[0], poles[poles.Count - 1]);
								item.val = null;
							}
							else
							{
								// always create open BSplines, because BSpline fiddels around with closed BSplines, SimplifyBSpline does a much better job
								if (poles != null)
								{
									BSpline bsp = BSpline.Construct();
									bsp.SetData(degree, poles.ToArray(), weights, knots.ToArray(), multiplicities.ToArray(), false);
									item.val = SimplifyBSpline(bsp, form, closed);
								}
								else
								{
									BSpline2D bsp2d = new BSpline2D(poles2d.ToArray(), weights, knots.ToArray(), multiplicities.ToArray(), degree, false, knots[0], knots[knots.Count - 1]);
									item.val = SimplifyBSpline2D(bsp2d, form, closed, eps);
								}
							}
						}
						break;
					case Item.ItemType.circle: // name, position, radius
						{

							object o = CreateEntity(item.parameter["position"]);
							if (o is FreeCoordSys) // which it always must be
							{
								FreeCoordSys fcs = (FreeCoordSys)o;
								Ellipse elli = Ellipse.Construct();
								double r = item.parameter["radius"].fval;
								elli.SetCirclePlaneCenterRadius(fcs.plane, fcs.Location, r);
								item.val = elli;
							}
							else if (o is Pair<GeoPoint2D, GeoVector2D> sys2)
							{
								double r = item.parameter["radius"].fval;
								item.val = new Circle2D(sys2.First, r);
							}
							else
							{
								item.val = null;
							}
						}
						break;
					case Item.ItemType.ellipse: // name, position, semi_axis_1, semi_axis_2
						{
							object o = CreateEntity(item.parameter["position"]);
							if (o is FreeCoordSys fcs) // which it always must be
							{
								Ellipse elli = Ellipse.Construct();
								double majorRadius = item.parameter["semi_axis_1"].fval;
								double minorRadius = item.parameter["semi_axis_2"].fval;
								elli.SetEllipseArcCenterAxis(fcs.Location, majorRadius * fcs.DirectionX.Normalized, minorRadius * fcs.DirectionY.Normalized, 0.0, 2 * Math.PI);
								item.val = elli;
							}
							else if (o is Pair<GeoPoint2D, GeoVector2D> sys2)
							{
								double majorRadius = item.parameter["semi_axis_1"].fval;
								double minorRadius = item.parameter["semi_axis_2"].fval;
								item.val = new Ellipse2D(sys2.First, majorRadius * sys2.Second, minorRadius * sys2.Second.ToLeft());
							}
							else
							{
								item.val = null;
							}
						}
						break;
					case Item.ItemType.polyline: // name, points
						{
							object opoints = CreateGeoPointList(item.parameter["points"].lval);
							List<GeoPoint> points = opoints as List<GeoPoint>;
							List<GeoPoint2D> points2d = opoints as List<GeoPoint2D>;
							if (points != null)
							{
								Polyline pl = Polyline.FromPoints(points.ToArray(), false);
								item.val = pl;
							}
							else
							{
								Polyline2D pl = new Polyline2D(points2d.ToArray());
								item.val = pl;
							}
						}
						break;
					case Item.ItemType.outerBoundaryCurve: // name, points
					case Item.ItemType.boundaryCurve: // name, segments, self_intersect
						{
							List<Item> segments = item.parameter["segments"].lval;
							List<ICurve> crvs = new List<ICurve>();
							List<StepEdgeDescriptor> res = new List<StepEdgeDescriptor>();
							for (int i = 0; i < segments.Count; i++)
							{
								object o = CreateEntity(segments[i]);
								if (o is ICurve)
								{
									StepEdgeDescriptor se = new StepEdgeDescriptor(o as ICurve);
									se.MakeVertices();
									res.Add(se);
								}
							}
							item.val = res;
						}
						break;
					case Item.ItemType.representationRelationship: // name, description, rep_1, rep_2
					case Item.ItemType.constructiveGeometryRepresentationRelationship:
					case Item.ItemType.shapeRepresentationRelationship:
					case Item.ItemType.representationRelationshipWithTransformation: // name, description, rep_1, rep_2, transformation_operator
						{
							object rep1 = CreateEntity(item.parameter["rep_1"] as Item);
							object rep2 = CreateEntity(item.parameter["rep_2"] as Item);
							List<Item> rep1list = item.parameter["rep_1"].parameter["items"].lval;
							List<Item> rep2list = item.parameter["rep_2"].parameter["items"].lval;
							string name = item.parameter["rep_1"].parameter["name"].sval;
							Item context1 = item.parameter["rep_1"].parameter["context_of_items"];
							Item context2 = item.parameter["rep_2"].parameter["context_of_items"];
							if (item.parameter.TryGetValue("transformation_operator", out Item transformationOperator))
							{
								if (!(item.parameter["rep_1"].parameter.TryGetValue("transformation_operator", out Item dumy)))
								{
									item.parameter["rep_1"].parameter["transformation_operator"] = transformationOperator;
								}
							}
							if (!context1.parameter.TryGetValue("_relationship", out Item rel1))
							{
								context1.parameter["_relationship"] = rel1 = new Item(Item.ItemType.created, new List<Item>());
							}
							rel1.lval.AddRange(item.parameter["rep_1"].parameter["items"].lval);
							if (!context2.parameter.TryGetValue("_relationship", out Item rel2))
							{
								context2.parameter["_relationship"] = rel2 = new Item(Item.ItemType.created, new List<Item>());
							}
							rel2.lval.AddRange(item.parameter["rep_2"].parameter["items"].lval);
							foreach (Item it in rep1list)
							{
								if (it.type == Item.ItemType.axis2Placement_3d)
								{
									CreateEntity(it);
									Item relatedItems;
									if (!it.parameter.TryGetValue("_relationship", out relatedItems))
									{
										it.parameter["_relationship"] = relatedItems = new Item(Item.ItemType.created, new List<Item>());
									}
									relatedItems.lval.AddRange(item.parameter["rep_2"].parameter["items"].lval);
								}
							}
							if (rep2 is GeoObjectList)
							{
								if ((item.parameter["rep_1"] as Item).val is GeoObjectList) ((item.parameter["rep_1"] as Item).val as GeoObjectList).AddRange(rep2 as GeoObjectList);
								else (item.parameter["rep_1"] as Item).val = rep2;
							}
							item.val = (item.parameter["rep_1"] as Item).val;
							if (item.val is GeoObjectList gol)
							{
								if (gol.Count == 1) namedGeoObjects[name] = gol[0];
							}
						}
						break;
					case Item.ItemType.itemDefinedTransformation: // name, description, transform_item_1, transform_item_2
						{
							object o1 = CreateEntity(item.parameter["transform_item_1"]);
							object o2 = CreateEntity(item.parameter["transform_item_2"]);
							if (o1 is FreeCoordSys fcs1 && o2 is FreeCoordSys fcs2)
							{
								item.val = ModOp.Fit(fcs1.Location, new GeoVector[] { fcs1.DirectionX, fcs1.DirectionY, fcs1.DirectionZ }, fcs2.Location, new GeoVector[] { fcs2.DirectionX, fcs2.DirectionY, fcs2.DirectionZ });
#if DEBUG
								System.Diagnostics.Trace.WriteLine("Transformation " + item.definingIndex.ToString() + ", " + ((ModOp)item.val).Matrix[0, 0].ToString() + ", " + ((ModOp)item.val).Matrix[0, 1].ToString() + ", " + ((ModOp)item.val).Matrix[0, 2].ToString()
									+ ", " + ((ModOp)item.val).Matrix[1, 0].ToString() + ", " + ((ModOp)item.val).Matrix[1, 1].ToString() + ", " + ((ModOp)item.val).Matrix[1, 2].ToString()
									+ ", " + ((ModOp)item.val).Matrix[2, 0].ToString() + ", " + ((ModOp)item.val).Matrix[2, 1].ToString() + ", " + ((ModOp)item.val).Matrix[2, 2].ToString());
#endif
							}
							else
								item.val = null;
						}
						break;
					case Item.ItemType.representation:
					case Item.ItemType.constructiveGeometryRepresentation:
					case Item.ItemType.shapeRepresentation: // name , items, context_of_items
					case Item.ItemType.geometricallyBoundedSurfaceShapeRepresentation:
					case Item.ItemType.geometricallyBoundedWireframeShapeRepresentation:
						{
							string name = item.parameter["name"].sval;
							GetContext(item.parameter["context_of_items"]);
							GeoObjectList lst = new GeoObjectList();
							List<FreeCoordSys> fcslist = new List<FreeCoordSys>();
							List<Item> items = item.parameter["items"].lval; // this is either a list of geoobjects or coordinate systems
																			 // I don't know, what the coordinate systems are good for
							for (int i = 0; i < items.Count; i++)
							{
								object o = CreateEntity(items[i]);
								if (o is IGeoObject) lst.Add(o as IGeoObject);
								else if (o is GeoObjectList) lst.AddRange(o as GeoObjectList);
								else if (o is FreeCoordSys) fcslist.Add((FreeCoordSys)o);
								else
								{

								}
							}
							if (lst.Count > 0) item.val = lst;
							else if (fcslist.Count > 0) item.val = fcslist;
							else item.val = null;
						}
						break;
					case Item.ItemType.contextDependentShapeRepresentation: //  representation_relation, represented_product_relation
						{
							// TODO: Debug TKStep project, follow STEPControl_ActorRead::TransferEntity (multiple definitions)
							// wenn beide Listen null sind, dann über represented_product_relation, nextAssemblyUsageOccurrence auf product gehen
							// und dort die Transformation anwenden
							Item repProdRel = item.parameter["represented_product_relation"];
							Item repRel = item.parameter["representation_relation"];
							if (repProdRel != null)
							{
								CreateEntity(repProdRel);
								CreateEntity(repRel);
								Item relatingProduct = repProdRel.parameter["definition"].parameter["relating_product_definition"].parameter["formation"].parameter["of_product"];
								Item relatedProduct = repProdRel.parameter["definition"].parameter["related_product_definition"].parameter["formation"].parameter["of_product"];
								if (!relatingProduct.parameter.ContainsKey("_children")) relatingProduct.parameter["_children"] = new Item(Item.ItemType.created, new List<Item>());
								Item child = new Item(Item.ItemType.created, null);
								ModOp transformation = ModOp.Identity;
								if (repRel.parameter.TryGetValue("transformation_operator", out Item transform))
								{
									Item rep1 = repRel.parameter["rep_1"];
									Item rep2 = repRel.parameter["rep_2"];
									transformation = GetTransformation(transform.parameter["transform_item_1"], transform.parameter["transform_item_2"], rep1, rep2);
								}
								else
								{

								}
								if (repRel.parameter["rep_1"].parameter["context_of_items"] == repRel.parameter["rep_2"].parameter["context_of_items"])
								{

								}
								child.parameter["_transformation"] = new Item(Item.ItemType.created, transformation);
								child.parameter["_referred"] = relatedProduct;
								relatingProduct.parameter["_children"].lval.Add(child);
								products.Add(relatingProduct);
							}
#if DEBUG
							//System.Diagnostics.Trace.WriteLine(SubTree(item.definingIndex, 5));
#endif
							//GeoObjectList golist1 = null;
							//GeoObjectList golist2 = null;
							//GeoObjectList golist = new GeoObjectList();
							//if (repRel != null)
							//{
							//    Item rep1 = repRel.parameter["rep_1"];
							//    Item rep2 = repRel.parameter["rep_2"];
							//    GeoObjectList list = new GeoObjectList();
							//    if (rep1 != null && rep2 != null)
							//    {
							//        List<Item> lrep1 = rep1.parameter["items"].lval;
							//        List<Item> lrep2 = rep2.parameter["items"].lval;
							//        if (lrep1.Count == 1 && lrep1[0].type == Item.ItemType.axis2Placement_3d && lrep1[0].parameter.ContainsKey("_relationship"))
							//        {
							//            Item rel = lrep1[0].parameter["_relationship"];
							//            if (rel.type == Item.ItemType.list)
							//            {
							//                for (int i = 0; i < rel.lval.Count; i++)
							//                {
							//                    Object o = CreateEntity(rel.lval[i]);
							//                    if (o is IGeoObject) list.Add(o as IGeoObject);
							//                    if (o is GeoObjectList) list.AddRange(o as GeoObjectList);
							//                }
							//            }
							//        }
							//        else
							//        {
							//            for (int i = 0; i < lrep1.Count; i++)
							//            {
							//                object o = CreateEntity(lrep1[i]);
							//                if (o is FreeCoordSys && lrep1[i].parameter.ContainsKey("_relationship"))
							//                {
							//                    Item rel = lrep1[i].parameter["_relationship"];
							//                    if (rel.type == Item.ItemType.list)
							//                    {
							//                        for (int ii = 0; ii < rel.lval.Count; ii++)
							//                        {
							//                            Object oo = CreateEntity(rel.lval[ii]);
							//                            if (oo is IGeoObject) list.Add(oo as IGeoObject);
							//                            if (oo is GeoObjectList) list.AddRange(oo as GeoObjectList);
							//                        }
							//                    }
							//                }
							//                if (o is IGeoObject) list.Add(o as IGeoObject);
							//                if (o is GeoObjectList) list.AddRange(o as GeoObjectList);
							//            }
							//        }
							//        for (int i = 0; i < lrep2.Count; i++)
							//        {
							//            if (lrep2[i].type == Item.ItemType.axis2Placement_3d)
							//            {
							//                ModOp m = GetTransformation(lrep1[0], lrep2[i], rep1, rep2);
							//                GeoObjectList lcloned = list.CloneObjects();
							//                lcloned.Modify(m);
							//                golist.AddRange(lcloned);
							//            }
							//            else
							//            {

							//            }
							//        }
							//    }
							//    if (rep1 != null) golist1 = CreateEntity(rep1) as GeoObjectList;
							//    if (rep2 != null) golist2 = CreateEntity(rep2) as GeoObjectList;
							//    if (golist2 != null) golist.AddRange(golist2);
							//    if (golist1 != null && repRel.parameter.TryGetValue("transformation_operator", out Item transform))
							//    {
							//        ModOp m = GetTransformation(transform.parameter["transform_item_1"], transform.parameter["transform_item_2"], rep1, rep2);
							//        golist1 = golist1.CloneObjects();
							//        golist1.Modify(m);
							//        // golist1.Modify(m);
							//        //  golist.AddRange(golist1);
							//    }
							//}
#if DEBUG
							//foreach (IGeoObject go in golist)
							//{
							//    go.UserData["StepImport.CDSR.ItemNumber"] = new UserInterface.IntegerProperty(item.definingIndex, "StepImport.CDSR.ItemNumber");
							//}
#endif
							item.val = null; //  golist;
						}
						break;
					case Item.ItemType.representationItem: // name
						{
							item.val = item.SubString(0);
						}
						break;
					case Item.ItemType.geometricRepresentationContext:// coordinate_space_dimension
						{
							int dim = (int)item.SubFloat(0);
						}
						break;
					case Item.ItemType.globalUncertaintyAssignedContext:
						{

						}
						break;
					case Item.ItemType.globalUnitAssignedContext: // Identifier, text, set of Unit
						{
							List<Item> subList = item.SubList(2);
							context resctx = null;
							for (int i = 0; i < subList.Count; i++)
							{
								object o = CreateEntity(subList[i]);
								if (o is context) resctx = o as context;
							}
							subList = item.SubList(3);
							for (int i = 0; i < subList.Count; i++)
							{
								object o = CreateEntity(subList[i]);
								if (o is context)
								{
									if (resctx != null) resctx.uncertainty = (o as context).uncertainty;
									else resctx = o as context;
								}
							}
							item.val = resctx;
						}
						break;
					case Item.ItemType.planeAngleMeasureWithUnit:
						item.val = CreateEntity(item.SubItem(0));
						break;
					case Item.ItemType.planeAngleMeasure:
						{
							context ctx = new context(); // if we need more information in context, we need to build more sophisticated class context
							ctx.toRadian = item.SubFloat(0);
							item.val = ctx;
						}
						break;
					case Item.ItemType.conversionBasedUnit: // dimensions, name, conversion_factor
						item.val = CreateEntity(item.SubItem(2));
						break;
					case Item.ItemType.uncertaintyMeasureWithUnit:
						if (item.val is List<Item>)
						{
							context ctxt = new context();
							ctxt.uncertainty = (item.val as List<Item>)[0].SubFloat(0);
							item.val = ctxt;
						}
						else item.val = null;
						break;
					case Item.ItemType.representationContext:
						{
							GetContext(item);
							//context ctxt = new context();
							//if (item.parameter["units"] != null)
							//{
							//    Item units = item.parameter["units"];
							//    foreach (Item subitem in units.lval)
							//    {
							//        if (subitem.parameter["name"] != null)
							//        {
							//            string sval = subitem.parameter["name"].sval;
							//            if (sval == "METRE")
							//            {
							//                ctxt.factor = 1000; // meter to mm
							//            }
							//        }
							//    }
							//}
							//item.val = ctxt;
						}
						break;
					case Item.ItemType.seamCurve:
					case Item.ItemType.surfaceCurve: // name, curve_3d, associated_geometry, master_representation
					case Item.ItemType.boundedCurve: // this is only a super-type with no additional properties
					case Item.ItemType.geometricRepresentationItem:
					case Item.ItemType.curve: // name, curve_3d, associated_geometry, master_representation
						{
							switch (item.parameter["master_representation"].sval)
							{
								case "CURVE_3D":
									item.val = CreateEntity(item.parameter["curve_3d"]);
									break;
								case "PCURVE_S1":
									if (item.parameter["associated_geometry"].lval[0].type == Item.ItemType.pcurve)
									{
										object o = CreateEntity(item.parameter["associated_geometry"].lval[0]);
										if (o is ICurve) item.val = o;
										else if (item.parameter.ContainsKey("curve_3d")) item.val = CreateEntity(item.parameter["curve_3d"]);
									}
									break;
								case "PCURVE_S2":
									if (item.parameter["associated_geometry"].lval[1].type == Item.ItemType.pcurve)
									{
										object o = CreateEntity(item.parameter["associated_geometry"].lval[1]);
										if (o is ICurve) item.val = o;
										else if (item.parameter.ContainsKey("curve_3d")) item.val = CreateEntity(item.parameter["curve_3d"]);
									}
									break;
							}
							if (item.parameter.TryGetValue("_startpoint", out Item startPoint) && item.parameter.TryGetValue("_endpoint", out Item endPoint) && item.parameter["master_representation"].sval != "CURVE_3D")
							{   // the curve was created as a PCURVE on the surface, but maybe the 3d curve would have been better
								// In some cases the step data suggest to use the 2d (PCURVE) representation, but the 3d representation is actually better. Then we choose to use the 3d representation.
								// _startpoint and _enpoint parameters have been added to the item in the edgeCurve creation.
								// maybe there is a bug with the conical surface 2d system, in v direction
								ICurve c3d = CreateEntity(item.parameter["curve_3d"]) as ICurve;
								if (c3d != null && c3d != item.val)
								{
									GeoPoint sp = (startPoint.val as Vertex).Position;
									GeoPoint ep = (endPoint.val as Vertex).Position;
									double dist3d = (c3d.StartPoint | sp) + (c3d.EndPoint | ep);
									if (c3d.IsClosed) dist3d = c3d.DistanceTo(sp) + c3d.DistanceTo(ep); // e.g. a circle on a conical surface
									double distPCurve = ((item.val as ICurve).StartPoint | sp) + ((item.val as ICurve).EndPoint | ep);
									if (dist3d < distPCurve) item.val = c3d;
								}

							}
						}
						break;
					case Item.ItemType.pcurve: // basis_surface, reference_to_curve
						{
							ISurface srf = CreateEntity(item.parameter["basis_surface"]) as ISurface;
							object o = CreateEntity(item.parameter["reference_to_curve"]);
							if (o is List<object> lst)
							{
								for (int i = 0; i < lst.Count; i++)
								{   // what would multiple items mean?
									if (lst[i] is ICurve2D || lst[i] is ICurve)
									{
										o = lst[i];
										break;
									}
								}
							}
							if (o is ICurve2D c2d)
							{
								if (c2d is Line2D l2d)
								{
									if (srf is NurbsSurface nsrf)
									{
										BoundingRect ext = nsrf.GetMaximumExtent();
										if (!ext.Contains(l2d.StartPoint) && !ext.Contains(l2d.EndPoint))
										{
											item.val = null; // this happens e.g. with "C.I6.30.000_6.stp". We better would use the 3d curve then
										}
									}
								}
								if (srf is NurbsSurface nurbsSurface && nurbsSurfacesWithModifiedKnots.TryGetValue(nurbsSurface, out ModOp2D modify2DCurve))
								{
									// this nurbs surface doesn't have the original 2d space. Move the 2d curve to the modified 2d space here.
									c2d = c2d.GetModified(modify2DCurve);
								}
								if (item.val != null) item.val = srf.Make3dCurve(c2d);
							}
							else if (o is ICurve c3d)
							{
								item.val = c3d;
							}
							else
							{
								item.val = null; // should not happen
							}
						}
						break;
					case Item.ItemType.definitionalRepresentation: // name, items, context_of_items
						{
							List<object> res = new List<object>();
							foreach (Item subitem in item.parameter["items"].lval)
							{
								object o = CreateEntity(subitem);
								if (o != null) res.Add(o);
							}
							item.val = res;
						}
						break;
					case Item.ItemType.presentationStyleAssignment:
						{
							List<Item> sublist = item.SubList(0);
							for (int i = 0; i < sublist.Count; i++)
							{
								object o = CreateEntity(sublist[i]);
								if (o is ColorDef)
								{
									item.val = o as ColorDef;
									break;
								}
							}
						}
						break;
					case Item.ItemType.presentationLayerAssignment: // name, description, assigned_items
						{
							string layerName = item.parameter["description"].sval;
							if (string.IsNullOrWhiteSpace(layerName)) layerName = item.parameter["name"].sval;
							Layer layer = new Layer(layerName);
							GeoObjectList list = new GeoObjectList();
							if (item.parameter.ContainsKey("assigned_items"))
							{
								List<Item> sublist = item.parameter["assigned_items"].lval;
								for (int i = 0; i < sublist.Count; i++)
								{
									object o = CreateEntity(sublist[i]);
									if (o is IGeoObject)
									{
										list.Add(o as IGeoObject);
									}
								}
							}
							item.val = new Pair<Layer, GeoObjectList>(layer, list);
						}
						break;
					case Item.ItemType.surfaceStyleUsage:
						item.val = CreateEntity(item.SubItem(1));
						break;
					case Item.ItemType.surfaceSideStyle:
						{
							string nm = item.SubString(0);
							List<Item> sublist = item.SubList(1);
							for (int i = 0; i < sublist.Count; i++)
							{
								object o = CreateEntity(sublist[i]);
								if (o is ColorDef)
								{
									item.val = o as ColorDef;
									break;
								}
							}
						}
						break;
					case Item.ItemType.surfaceStyleFillArea:
						item.val = CreateEntity(item.SubItem(0));
						break;
					case Item.ItemType.fillAreaStyle:
						{
							string nm = item.SubString(0);
							List<Item> sublist = item.SubList(1);
							for (int i = 0; i < sublist.Count; i++)
							{
								object o = CreateEntity(sublist[i]);
								if (o is ColorDef)
								{
									item.val = o as ColorDef;
									break;
								}
							}
						}
						break;
					case Item.ItemType.fillAreaStyleColour: //name, fill_colour
						{
							string nm = item.parameter["name"].sval;
							ColorDef cd = CreateEntity(item.parameter["fill_colour"]) as ColorDef;
							if (cd != null)
							{
								if (!String.IsNullOrWhiteSpace(nm))
								{
									item.val = new ColorDef(nm, cd.Color);
									AvoidDuplicateColorNames(item.val as ColorDef);
								}
								else
								{
									item.val = cd;
								}
							}
						}
						break;
					case Item.ItemType.colourRgb:
						{
							string name = item.parameter["name"].sval;
							int red = (int)Math.Round(item.parameter["red"].fval * 255);
							int green = (int)Math.Round(item.parameter["green"].fval * 255);
							int blue = (int)Math.Round(item.parameter["blue"].fval * 255);
							if (string.IsNullOrWhiteSpace(name)) name = "rgb:" + red.ToString() + "_" + green.ToString() + "_" + blue.ToString();
							item.val = new ColorDef(name, Color.FromArgb(red, green, blue));
							AvoidDuplicateColorNames(item.val as ColorDef);
						}
						break;
					case Item.ItemType.pointStyle:
						item.val = null;
						break;
					case Item.ItemType.parameterValue:
						{
							// item.val already is a double, nothing to do 
						}
						break;
					case Item.ItemType.trimmedCurve: // name, basis_curve, trim_1, trim_2, sense_agreement, master_representation
						{
							string nm = item.SubString(0);
							object oo = CreateEntity(item.SubItem(1));
							if (oo is ICurve basisCurve)
							{
								basisCurve = basisCurve.Clone();
								double startParameter = double.MinValue;
								double endParameter = double.MinValue;
								GeoPoint startPoint = GeoPoint.Invalid;
								GeoPoint endPoint = GeoPoint.Invalid;
								if (item.SubItem(2).type == Item.ItemType.list)
								{
									List<Item> lst = (item.SubItem(2).val as List<Item>);
									for (int i = 0; i < lst.Count; i++)
									{
										object o = CreateEntity(lst[i]);
										if (o is List<Item>) o = (o as List<Item>)[0].val;
										if (o is double) startParameter = (double)o;
										if (o is GeoPoint) startPoint = (GeoPoint)o;
									}
								}
								if (item.SubItem(3).type == Item.ItemType.list)
								{
									List<Item> lst = (item.SubItem(3).val as List<Item>);
									for (int i = 0; i < lst.Count; i++)
									{
										object o = CreateEntity(lst[i]);
										if (o is List<Item>) o = (o as List<Item>)[0].val;
										if (o is double) endParameter = (double)o;
										if (o is GeoPoint) endPoint = (GeoPoint)o;
									}
								}
								bool sense = item.SubBool(4);
								string masterRepresentation = item.SubString(5);
								if (startParameter != double.MinValue && masterRepresentation != "CARTESIAN")
								{
									// trimmed curve defined by start- and endParameter
									if (basisCurve is Ellipse)
									{
										if (context != null)
										{
											startParameter = context.toRadian * startParameter;
											endParameter = context.toRadian * endParameter;
										}
									}
									if (!sense) basisCurve.Reverse();
									if (startParameter != endParameter) basisCurve.Trim(basisCurve.ParameterToPosition(startParameter), basisCurve.ParameterToPosition(endParameter));
									item.val = basisCurve;
								}
								else if (startPoint.IsValid && endPoint.IsValid)
								{
									// trimmed curve defined by start- and endpoint
									if (!sense) basisCurve.Reverse();
									double startPos = basisCurve.PositionOf(startPoint);
									double endPos = basisCurve.PositionOf(endPoint);
									basisCurve.Trim(startPos, endPos);
									item.val = basisCurve;
								}
							}
							else if (oo is ICurve2D basisCurve2d)
							{
								basisCurve2d = basisCurve2d.Clone();
								double startParameter = double.MinValue;
								double endParameter = double.MinValue;
								GeoPoint2D startPoint = GeoPoint2D.Invalid;
								GeoPoint2D endPoint = GeoPoint2D.Invalid;
								if (item.SubItem(2).type == Item.ItemType.list)
								{
									List<Item> lst = (item.SubItem(2).val as List<Item>);
									for (int i = 0; i < lst.Count; i++)
									{
										object o = CreateEntity(lst[i]);
										if (o is List<Item>) o = (o as List<Item>)[0].val;
										if (o is double) startParameter = (double)o;
										if (o is GeoPoint2D) startPoint = (GeoPoint2D)o;
									}
								}
								if (item.SubItem(3).type == Item.ItemType.list)
								{
									List<Item> lst = (item.SubItem(3).val as List<Item>);
									for (int i = 0; i < lst.Count; i++)
									{
										object o = CreateEntity(lst[i]);
										if (o is List<Item>) o = (o as List<Item>)[0].val;
										if (o is double) endParameter = (double)o;
										if (o is GeoPoint2D) endPoint = (GeoPoint2D)o;
									}
								}
								bool sense = item.SubBool(4);
								string masterRepresentation = item.SubString(5);
								if (startParameter != double.MinValue && masterRepresentation != "CARTESIAN")
								{
									// trimmed curve defined by start- and endParameter
									if (basisCurve2d is Ellipse2D elli2d)
									{
										if (context != null)
										{
											startParameter = context.toRadian * startParameter;
											endParameter = context.toRadian * endParameter;
										}
										elli2d.Trim(startParameter / (2 * Math.PI), endParameter / (2 * Math.PI));
									}
									else
									{   // are all other curves from 0 to 1 ??? there is no "ParameterToPosition" for 2d curves
										// basisCurve2d.Trim(basisCurve2d.ParameterToPosition(startParameter), basisCurve.ParameterToPosition(endParameter));
										basisCurve2d.Trim(startParameter, endParameter);
									}
									if (!sense) basisCurve2d.Reverse();
									item.val = basisCurve2d;
								}
								else if (startPoint.IsValid && endPoint.IsValid)
								{
									// trimmed curve defined by start- and endpoint
									double startPos = basisCurve2d.PositionOf(startPoint);
									double endPos = basisCurve2d.PositionOf(endPoint);
									basisCurve2d.Trim(startPos, endPos);
									if (!sense) basisCurve2d.Reverse();
									item.val = basisCurve2d;
								}
							}
						}
						break;
					case Item.ItemType.mappedItem:
						{
#if DEBUG
							//System.Diagnostics.Trace.WriteLine(" ----- ");
							//System.Diagnostics.Trace.WriteLine(SubTree(item.definingIndex, 5));
							//string[] up = UsagePaths(item);
							//for (int i = 0; i < up.Length; i++)
							//{
							//    System.Diagnostics.Trace.WriteLine(up[i]);
							//}
#endif
							mappedItems.Add(item);
							string name = item.parameter["name"].sval;
							object mo = CreateEntity(item.parameter["mapping_source"].parameter["mapping_origin"]);
							object mr = CreateEntity(item.parameter["mapping_source"].parameter["mapped_representation"]);
							object mt = CreateEntity(item.parameter["mapping_target"]);
							if (mo is FreeCoordSys && mt is FreeCoordSys)
							{
								ModOp trsf = ModOp.Fit((FreeCoordSys)mo, (FreeCoordSys)mt);
								if (mr is IGeoObject)
								{
									IGeoObject modified = (mr as IGeoObject).Clone();
									modified.Modify(trsf);
									item.val = modified;
								}
								else if (mr is GeoObjectList)
								{
									GeoObjectList modified = (mr as GeoObjectList).CloneObjects();
									modified.Modify(trsf);
									if (modified.Count > 1 && !String.IsNullOrEmpty(item.parameter["mapping_source"].parameter["mapped_representation"].parameter["name"].sval))
									{
										Block blk = Block.Construct();
										blk.Set(modified);
										blk.Name = item.parameter["mapping_source"].parameter["mapped_representation"].parameter["name"].sval;
										item.val = blk;
									}
									else
									{
										item.val = modified;
									}
								}
								if (!item.parameter.ContainsKey("_children")) item.parameter["_children"] = new Item(Item.ItemType.created, new List<Item>());
								item.parameter["_children"].lval.Add(item.parameter["mapping_source"].parameter["mapped_representation"]);
								item.parameter["_transformation"] = new Item(Item.ItemType.created, trsf);
							}
						}
						break;
					case Item.ItemType.draughtingModel: // name,*items,context_of_items
						{
							string name = item.parameter["name"].sval;
							List<Item> items = item.parameter["items"].lval;
							for (int i = 0; i < items.Count; i++)
							{
								object o = CreateEntity(items[i]);
							}
							item.val = items;
							break;
						}
					default:
						{
#if DEBUG
							System.Diagnostics.Trace.WriteLine("STEP: not imported. " + item.type.ToString());
#endif
							importProblems[item.definingIndex] = "item not imported: " + item.type.ToString();
							break;
						}
				}
#if DEBUG
				lock (definitionStack) definitionStack.Pop();
#endif
				return item.val; // has now been created
			} // end lock
		}

		private void AvoidDuplicateColorNames(ColorDef cd)
		{   // some step files name all their colors with the same name (e.g. "Colour"). The name is the key for the ColorDef, so all colors would be replaced by the first color with this name
			lock (this)
			{
				if (definedColors == null) definedColors = new Dictionary<string, ColorDef>();
			}
			lock (definedColors)
			{
				if (definedColors.TryGetValue(cd.Name, out ColorDef found))
				{
					if (found.Color != cd.Color)
					{   // same name but different colors
						cd.Name = cd.Name + " (rgb:" + cd.Color.R.ToString() + "_" + cd.Color.G.ToString() + "_" + cd.Color.B.ToString() + ")";
						definedColors[cd.Name] = cd;
					}
				}
				else
				{
					definedColors[cd.Name] = cd;
				}
			}
		}

		private void CreatingFace()
		{
			Interlocked.Increment(ref createdFaces);
			FrameImpl.MainFrame?.UIService.ShowProgressBar(true, 100.0 * createdFaces / numFaces);
		}

		private ModOp GetTransformation(Item origin, Item target, Item origContext, Item targContext)
		{   // implemented analogous to OpenCascade: STEPControl_ActorRead::ComputeTransformation 
			FreeCoordSys org = (FreeCoordSys)CreateEntity(origin);
			FreeCoordSys trg = (FreeCoordSys)CreateEntity(target);
			int code1 = 0, code2 = 0;
			foreach (Item item in origContext.parameter["items"].lval)
			{
				if (item == origin) code1 = 1;
				else if (item == target) code1 = -1;
				if (code1 != 0) break;
			}
			foreach (Item item in targContext.parameter["items"].lval)
			{
				if (item == origin) code2 = -1;
				else if (item == target) code2 = 1;
				if (code2 != 0) break;
			}
			if (code1 != 1 && code2 != 1)
			{
				if (code1 == -1 && code2 == -1)
				{
					// swap origin and target
					FreeCoordSys tmp = org;
					org = trg;
					trg = tmp;
				}
				else
				{   // this should not happen, since a axis2Placement_3d has not been found neither in origin nor in target
					// it is an error in the step file
				}
			}
#if DEBUG
			//DebuggerContainer dc = new DebuggerContainer();
			//ColorDef cdr = new ColorDef("from", Color.Red);
			//ColorDef cdb = new ColorDef("to", Color.Blue);
			//{
			//    Polyline pl = Polyline.Construct();
			//    pl.SetPoints(new GeoPoint[] { org.Location, org.Location + 5 * org.DirectionX, org.Location + 5 * org.DirectionY }, true);
			//    pl.ColorDef = cdr;
			//    dc.Add(pl);
			//    pl = Polyline.Construct();
			//    pl.SetPoints(new GeoPoint[] { trg.Location, trg.Location + 5 * trg.DirectionX, trg.Location + 5 * trg.DirectionY }, true);
			//    pl.ColorDef = cdb;
			//    dc.Add(pl);
			//}
#endif
			// von blau nach rot gem. axis drehen (um axis^axis), dann in rot refax blau nach refax rot drehen um axis rot.
			double lfo = GetContextLengthFactor(origContext.parameter["context_of_items"]);
			double lft = GetContextLengthFactor(targContext.parameter["context_of_items"]);
			if (lfo != 1.0)
			{
				org.Location.x = lfo * org.Location.x;
				org.Location.y = lfo * org.Location.y;
				org.Location.z = lfo * org.Location.z;
			}
			if (lft != 1.0)
			{
				trg.Location.x = lft * trg.Location.x;
				trg.Location.y = lft * trg.Location.y;
				trg.Location.z = lft * trg.Location.z;
			}
			//CultureInfo c = CultureInfo.CreateSpecificCulture("en-US");
			//System.Diagnostics.Trace.WriteLine("ax3Orig (" + org.Location.x.ToString("F3", c) + ", " + org.Location.y.ToString("F3", c) + ", " + org.Location.z.ToString("F3", c) + ") (" + org.DirectionZ.x.ToString("F3", c) + ", " + org.DirectionZ.y.ToString("F3", c) + ", " + org.DirectionZ.z.ToString("F3", c) + ") (" + org.DirectionX.x.ToString("F3", c) + ", " + org.DirectionX.y.ToString("F3", c) + ", " + org.DirectionX.z.ToString("F3", c) + ") (" + org.DirectionY.x.ToString("F3", c) + ", " + org.DirectionY.y.ToString("F3", c) + ", " + org.DirectionY.z.ToString("F3", c) + ")");
			//System.Diagnostics.Trace.WriteLine("ax3Targ (" + trg.Location.x.ToString("F3", c) + ", " + trg.Location.y.ToString("F3", c) + ", " + trg.Location.z.ToString("F3", c) + ") (" + trg.DirectionZ.x.ToString("F3", c) + ", " + trg.DirectionZ.y.ToString("F3", c) + ", " + trg.DirectionZ.z.ToString("F3", c) + ") (" + trg.DirectionX.x.ToString("F3", c) + ", " + trg.DirectionX.y.ToString("F3", c) + ", " + trg.DirectionX.z.ToString("F3", c) + ") (" + trg.DirectionY.x.ToString("F3", c) + ", " + trg.DirectionY.y.ToString("F3", c) + ", " + trg.DirectionY.z.ToString("F3", c) + ")");
			ModOp res;
			{   // gp_Trsf::SetTransformation 
				FreeCoordSys FromA1 = trg;
				FreeCoordSys ToA2 = org;
				Matrix matrix = DenseMatrix.OfRowArrays(ToA2.DirectionX, ToA2.DirectionY, ToA2.DirectionZ);
				Vector loc = new DenseVector(new double[] { ToA2.Location.x, ToA2.Location.y, ToA2.Location.z });
				//matrix.Transpose();
				loc = (Vector)(matrix * loc);
				loc[0] = -loc[0]; loc[1] = -loc[1]; loc[2] = -loc[2];

				Matrix MA1 = DenseMatrix.OfRowArrays(FromA1.DirectionX, FromA1.DirectionY, FromA1.DirectionZ);
				MA1.Transpose();
				Vector MA1loc = new DenseVector(new double[] { FromA1.Location.x, FromA1.Location.y, FromA1.Location.z });
				MA1loc = (Vector)(matrix * MA1loc);
				loc = (Vector)(loc + MA1loc);
				matrix = (Matrix)(MA1 * matrix).Transpose(); // introduced ".Transpose()" because of "FMC1_Rev2.0.stp"
				res = new ModOp(matrix.ToArray(), new GeoVector(loc[0], loc[1], loc[2]));
			}
			if (transformationMode) //trg.Location == GeoPoint.Origin)
			{
				res = ModOp.Translate(org.Location - trg.Location) * ModOp.Fit(new GeoVector[] { trg.DirectionX, trg.DirectionY, trg.DirectionZ }, new GeoVector[] { org.DirectionX, org.DirectionY, org.DirectionZ });
			}
			else
			{
				//res = ModOp.Fit(org, trg);
			}
			//System.Diagnostics.Trace.WriteLine("Trsf (" + res.Item(0, 0).ToString("F3", c) + ", " + res.Item(0, 1).ToString("F3", c) + ", " + res.Item(0, 2).ToString("F3", c) + ", " + res.Item(0, 3).ToString("F3", c) + ")");
			//System.Diagnostics.Trace.WriteLine("     (" + res.Item(1, 0).ToString("F3", c) + ", " + res.Item(1, 1).ToString("F3", c) + ", " + res.Item(1, 2).ToString("F3", c) + ", " + res.Item(1, 3).ToString("F3", c) + ")");
			//System.Diagnostics.Trace.WriteLine("     (" + res.Item(2, 0).ToString("F3", c) + ", " + res.Item(2, 1).ToString("F3", c) + ", " + res.Item(2, 2).ToString("F3", c) + ", " + res.Item(2, 3).ToString("F3", c) + ")");
			return res;

			//Unreachable code
			/*
            // according to pdmug_release4_3.pdf, page 52, but not used
            GeoVector zo = (GeoVector)origin.parameter["axis"].val;
            GeoVector ao = (GeoVector)origin.parameter["ref_direction"].val;
            GeoVector xo = ao - (ao * zo) * zo;
            GeoVector yo = zo ^ xo;
            Matrix A = DenseMatrix.OfRowArrays(xo, yo, zo);
            GeoVector zt = (GeoVector)target.parameter["axis"].val;
            GeoVector at = (GeoVector)target.parameter["ref_direction"].val;
            GeoVector xt = at - (at * zt) * zt;
            GeoVector yt = zt ^ xt;
            Matrix B = DenseMatrix.OfRowArrays(xt, yt, zt);
            B = (Matrix)B.Transpose();
            Matrix C = (Matrix)(B * A);
            GeoPoint t = (GeoPoint)origin.parameter["location"].val;
            GeoPoint u = (GeoPoint)target.parameter["location"].val;
            t.x *= lfo;
            t.y *= lfo;
            t.z *= lfo;
            u.x *= lft;
            u.y *= lft;
            u.z *= lft;
            Vector v = (Vector)(C * new DenseVector(new double[] { t.x, t.y, t.z }));
            GeoVector trs = new GeoVector(u.x - v[0], u.y - v[1], u.z - v[2]);
            res = new ModOp(C.ToArray(), trs);
            // return res;

            //int code1 = 0, code2 = 0;
            foreach (Item item in origContext.parameter["items"].lval)
            {
                if (item == origin) code1 = 1;
                else if (item == target) code1 = -1;
                if (code1 != 0) break;
            }
            foreach (Item item in targContext.parameter["items"].lval)
            {
                if (item == origin) code2 = -1;
                else if (item == target) code2 = 1;
                if (code2 != 0) break;
            }
            if (code1 != 1 && code2 != 1)
            {
                if (code1 == -1 && code2 == -1)
                {
                    // swap origin and target
                    FreeCoordSys tmp = org;
                    org = trg;
                    trg = tmp;
                }
                else
                {   // this should not happen, since a axis2Placement_3d has not been found neither in origin nor in target
                    // it is an error in the step file
                }
            }
            if (lft != lfo)
            {

            }
            GeoVector cross = trg.DirectionZ ^ org.DirectionZ;
            if (cross.IsNullVector()) return ModOp.Fit(org, trg);
            GeoVector orgperp = cross ^ org.DirectionZ;
            GeoVector trgperp = cross ^ trg.DirectionZ;
            ModOp rot = ModOp.Fit(new GeoVector[] { org.DirectionZ, cross, -orgperp }, new GeoVector[] { trg.DirectionZ, cross, trgperp });
            ModOp trans = ModOp.Translate(org.Location - trg.Location);
            return trans * rot;
            */
		}

		private double GetContextLengthFactor(Item item)
		{
			Item units = item.parameter["units"];
			if (units != null && units.type == Item.ItemType.list)
			{
				foreach (Item it in units.val as List<Item>)
				{
					if (it.parameter.TryGetValue("conversion_factor", out Item cf))
					{
						if (cf.parameter.TryGetValue("value_component", out Item vc))
						{
							if (cf.type == Item.ItemType.lengthMeasureWithUnit && vc.val is List<Item> && (vc.val as List<Item>)[0].type == Item.ItemType.floatval)
							{
								double f = (vc.val as List<Item>)[0].fval;
								if (cf.parameter.TryGetValue("unit_component", out Item uc))
								{

								}
								return f; // * 1000; // why * 1000.0 ??? definitely wrong with AM3024-0x00.stp
							}
						}
					}
				}
			}
			return 1.0;
		}

		private void GetContext(Item item)
		{
			if (item == null) return;
			if (context == null)
			{
				context = new context();
				context.toRadian = 1.0;
				context.factor = 1.0;
			}

			if (item.parameter.TryGetValue("uncertainty", out Item uncertainty) && uncertainty.type == Item.ItemType.list)
			{
				foreach (Item it in uncertainty.val as List<Item>)
				{
					if (it.parameter.TryGetValue("value_component", out Item vc))
					{   // LENGTH_MEASURE is not a used type
						if (vc.val is List<Item> && (vc.val as List<Item>)[0].type == Item.ItemType.floatval) context.uncertainty = (double)(vc.val as List<Item>)[0].val;
					}
				}
			}
			if (item.parameter.TryGetValue("units", out Item units) && units.type == Item.ItemType.list)
			{
				foreach (Item it in units.val as List<Item>)
				{
					if (it.parameter.TryGetValue("conversion_factor", out Item cf) &&
						cf.parameter.TryGetValue("value_component", out Item vc) &&
						vc.val is List<Item> vcList && vcList.Count > 0 &&
						vcList[0].type == Item.ItemType.floatval)
					{
						double value = (double)vcList[0].val;

						if (cf.type == Item.ItemType.planeAngleMeasure || cf.type == Item.ItemType.planeAngleMeasureWithUnit)
						{
							context.toRadian = value;
						}
						else if (cf.type == Item.ItemType.positiveLengthMeasure || cf.type == Item.ItemType.lengthMeasureWithUnit)
						{
							context.factor = value;

							if (it.parameter.TryGetValue("name", out Item name) &&
								name.type == Item.ItemType.stringval && name.sval == "METRE")
							{
								context.factor *= 1000; // Handle "METRE" case
							}
						}
					}
					else if (it.parameter.TryGetValue("name", out Item name))
					{
						// added because of issue 183.
						if (it.parameter.TryGetValue("prefix", out Item prefix) &&
							name.type == Item.ItemType.keyword && name.sval == "METRE")
						{
							if (prefix.sval == "MILLI")
								context.factor = 1;
							else if (prefix.sval == "CENTI")
								context.factor = 10;
							else if (prefix.sval == "DECI")
								context.factor = 100;
							else if (prefix.sval == null)
								context.factor = 1000; // Default case for "METRE" without prefix
						}
						else if (name.type == Item.ItemType.keyword && name.sval == "METRE")
						{
							// added because of "issue153.stp"
							context.factor = 1000; // Default case for "METRE"
						}
					}
				}
			}
		}

		private ICurve2D SimplifyBSpline2D(BSpline2D bsp2d, BSplineCurveForm form, bool closed, double precision)
		{
			if (bsp2d.Degree == 1 && bsp2d.Poles.Length == 2) return new Line2D(bsp2d.Poles[0], bsp2d.Poles[1]);
			if (bsp2d.Multiplicities[0] <= bsp2d.Degree)
			{   // normally this multiplicity should be degree+1
				int n = 10;
				GeoPoint2D[] pnts = new GeoPoint2D[n];
				for (int i = 0; i < n; i++)
				{
					pnts[i] = bsp2d.PointAt((i + 1) * (1.0 / n + 2));
				}
				double error = Geometry.CircleFit(pnts, out GeoPoint2D cnt, out double radius);
				if (error < precision)
				{   // we need an arc with the correct orientation
					return new Arc2D(cnt, radius, (bsp2d.StartPoint - cnt).Angle, Geometry.OnLeftSide(cnt, bsp2d.StartPoint, bsp2d.StartDirection) ? SweepAngle.Full : SweepAngle.FullReverse);
				}
			}
			if (bsp2d.GetSimpleCurve(precision, out ICurve2D simpleCurve))
			{
				return simpleCurve; // line or arc
			}
			return bsp2d;
		}
		private ICurve SimplifyBSpline(BSpline bsp, BSplineCurveForm form, bool closed)
		{
			switch (form)
			{
				case BSplineCurveForm.Circular_Arc:
					{
						if (Precision.IsEqual(bsp.Poles[0], bsp.Poles[bsp.PoleCount - 1]))
						{
							Ellipse elli = Ellipse.Construct();
							elli.SetCircle3Points((bsp as ICurve).PointAt(0.0), (bsp as ICurve).PointAt(0.333), (bsp as ICurve).PointAt(0.666), Plane.XYPlane);
							return elli;
						}
						else
						{
							Ellipse elli = Ellipse.Construct();
							elli.SetArc3Points((bsp as ICurve).PointAt(0.0), (bsp as ICurve).PointAt(0.5), (bsp as ICurve).PointAt(1.0), Plane.XYPlane);
							return elli;
						}
					}
				default:
					if (bsp.degree == 1)
					{
						if (bsp.PoleCount == 2) return Line.MakeLine(bsp.Poles[0], bsp.Poles[1]);
						else return Polyline.FromPoints(bsp.Poles, closed);

					}
					if (closed && bsp.Multiplicities[0] <= bsp.degree)
					{   // maybe this is a kind of nurbs which can safely be trimmed at the second and second last knots. this makes the b-spline closed
						// and removes overlapping parts. Overlapping parts are bad, because PositionOf is ambiguous.
						GeoPoint k1 = bsp.PointAtParam(bsp.Knots[1]);
						GeoPoint k2 = bsp.PointAtParam(bsp.Knots[bsp.Knots.Length - 2]);
						if (Precision.IsEqual(k1, k2))
						{
							BSpline bsp1 = bsp.TrimParam(bsp.Knots[1], bsp.Knots[bsp.Knots.Length - 2]);
							return bsp1;
						}
						else if (bsp.Knots.Length > 6)
						{
							k1 = bsp.PointAtParam(bsp.Knots[2]);
							k2 = bsp.PointAtParam(bsp.Knots[bsp.Knots.Length - 3]);
							if (Precision.IsEqual(k1, k2))
							{
								BSpline bsp1 = bsp.TrimParam(bsp.Knots[2], bsp.Knots[bsp.Knots.Length - 3]);
								return bsp1;
							}
						}
						double[] si = (bsp as ICurve).GetSelfIntersections();
						if (si.Length == 2)
						{
							BSpline bsp1 = bsp.Clone() as BSpline;
							(bsp1 as ICurve).Trim(si[0], si[1]);
							return bsp1;
						}
					}
					double poleLength = 0.0;
					for (int i = 0; i < bsp.PoleCount - 1; i++)
					{
						poleLength += bsp.Poles[i] | bsp.Poles[i + 1];
					}
					poleLength /= bsp.PoleCount;
					int knotCount = bsp.Knots.Length;
					double knotLength = (bsp.Knots[knotCount - 1] - bsp.Knots[0]) / knotCount;
					// the following corrects some BSplines, which have the first or last poles or knots very close to each other. This makes the start-direction or end-direction
					// very unstable, even opposite to the "real" direction. Since we rely on this direction, we need to correct these BSplines (like in MO21775-001-00.stp)
					if ((bsp.Poles[0] | bsp.Poles[1]) < poleLength * 1e-3 || (bsp.Poles[bsp.PoleCount - 1] | bsp.Poles[bsp.PoleCount - 2]) < poleLength * 1e-3 ||
						(bsp.Knots[1] - bsp.Knots[0]) < knotLength * 1e-3 || (bsp.Knots[knotCount - 1] - bsp.Knots[knotCount - 2]) < knotLength * 1e-3)
					{
						int n = bsp.PoleCount + bsp.degree;
						GeoPoint[] throughPoints = new GeoPoint[n];
						for (int i = 0; i < n; i++)
						{
							throughPoints[i] = (bsp as ICurve).PointAt(i / (double)(n - 1));
						}
						BSpline res = BSpline.Construct();
						if (res.ThroughPoints(throughPoints, 3, closed)) return res;
					}
					if (closed) bsp.IsClosed = true;
					return bsp;
			}
		}

		private ISurface MakeNurbsSurface(GeoPoint[,] poles, double[,] weights, double[] uKnots, double[] vKnots,
			int[] uMults, int[] vMults, int uDegree, int vDegree, bool uClosed, bool vClosed, BSplineSurfaceForm form)
		{
			// some NURBS surfaces come with extreme small uKnots or vKnots span. For better numerical behavior we normalize it here
			// this doesn't change the surface but only the 2d parameter space of the surface
			// an other case were we need to modify the knots span is with periodic surfaces that do not start with 0 as the first knot.
			ModOp2D knotModification = ModOp2D.Identity;
			if (uKnots[uKnots.Length - 1] - uKnots[0] < 0.5 || (uClosed && uKnots[0] != 0.0))
			{
				double f = 1.0 / (uKnots[uKnots.Length - 1] - uKnots[0]);
				double u0 = uKnots[0];
				for (int i = 0; i < uKnots.Length; i++)
				{
					uKnots[i] = (uKnots[i] - u0) * f;
				}
				knotModification[0, 0] = f;
				knotModification[0, 2] = -u0;
			}
			if (vKnots[vKnots.Length - 1] - vKnots[0] < 0.5 || (vClosed && vKnots[0] != 0.0))
			{
				double f = 1.0 / (vKnots[vKnots.Length - 1] - vKnots[0]);
				double v0 = vKnots[0];
				for (int i = 0; i < vKnots.Length; i++)
				{
					vKnots[i] = (vKnots[i] - v0) * f;
				}
				knotModification[1, 1] = f;
				knotModification[1, 2] = -v0;
			}
			NurbsSurface res = new NurbsSurface(poles, weights, uKnots, vKnots, uMults, vMults, uDegree, vDegree, false, false);
			if (!knotModification.IsIdentity)
			{
				nurbsSurfacesWithModifiedKnots[res] = knotModification; // remember the modification for later use
			}
			// we should not make canonical surfaces here, because a nurbs surface in a "GEOMETRIC_SET" used in a "GEOMETRICALLY_BOUNDED_SURFACE_SHAPE_REPRESENTATION"
			// uses nurbs surfaces as faces with natural bounds
			switch (form)
			{
				case BSplineSurfaceForm.Conical_Surf:
				case BSplineSurfaceForm.Generalised_Cone:
					{
						ConicalSurface cs = res.ConvertToCone(Precision.eps);
						if (cs != null) return cs;
					}
					break;
				case BSplineSurfaceForm.Plane_Surf:
					{
						PlaneSurface ps = res.ConvertToPlane(Precision.eps);
						if (ps != null) return ps;
					}
					break;
				default:
					if (uClosed)
					{
						if (uMults[0] <= uDegree)
						{   // the same should be here for vClosed, but TrimmV must be implemented
							// this is an self-overlapping surface
							BSpline crv = res.FixedV((vKnots[0] + vKnots[vKnots.Length - 1]) / 2, uKnots[0], uKnots[uKnots.Length - 1]) as BSpline;
							GeoPoint k1 = crv.PointAtParam(crv.Knots[crv.Knots.Length - 2]);
							GeoPoint k2 = crv.PointAtParam(crv.Knots[1]);
							if (Precision.IsEqual(k1, k2))
								res = res.TrimmU(uKnots[1], uKnots[uKnots.Length - 2]);
							else
							{ }
						}
					}
					if (vClosed)
					{
						if (vMults[0] <= vDegree)
						{   // the same should be here for vClosed, but TrimmV must be implemented
							// this is an self-overlapping surface
							BSpline crv = res.FixedU((uKnots[0] + uKnots[uKnots.Length - 1]) / 2, vKnots[0], vKnots[vKnots.Length - 1]) as BSpline;
							GeoPoint k1 = crv.PointAtParam(crv.Knots[crv.Knots.Length - 2]);
							GeoPoint k2 = crv.PointAtParam(crv.Knots[1]);
							if (Precision.IsEqual(k1, k2))
								res = res.TrimmV(vKnots[1], vKnots[vKnots.Length - 2]);
						}
					}
					res.SetPeriodic(uClosed, vClosed); // set periodic now for all closed surfaces, was only for non trimmed surfaces before
					break;
			}

			double uMinRestrict = 0.0, uMaxRestrict = 0.0, vMinRestrict = 0.0, vMaxRestrict = 0.0; // restriction for periodic                 
			if (uClosed)
			{
				double v = (vKnots[0] + vKnots[vKnots.Length - 1]) / 2.0;
				ICurve fv = res.FixedV(v, res.UKnots[0], res.UKnots[res.UKnots.Length - 1]);
				double[] si = fv.GetSelfIntersections();
				int bestPair = -1;
				double minDist = double.MaxValue;
				for (int i = 0; i < si.Length; i += 2)
				{
					double d = fv.PointAt(si[i]) | fv.PointAt(si[i + 1]);
					if (d < minDist)
					{
						minDist = d;
						bestPair = i;
					}
				}
				if (bestPair >= 0)
				{
					uMinRestrict = fv.PositionToParameter(si[bestPair]);
					uMaxRestrict = fv.PositionToParameter(si[bestPair + 1]);
				}
			}
			if (vClosed)
			{
				double u = (uKnots[0] + uKnots[uKnots.Length - 1]) / 2.0;
				ICurve fu = res.FixedU(u, res.VKnots[0], res.VKnots[res.VKnots.Length - 1]);
				double[] si = fu.GetSelfIntersections();
				int bestPair = -1;
				double minTan = double.MaxValue;
				for (int i = 0; i < si.Length; i += 2)
				{   // when there are multiple self intersection pairs, then it looks like the best pair is where the intersection is tangential
					double d = fu.PointAt(si[i]) | fu.PointAt(si[i + 1]);
					bool sd = Precision.SameDirection(fu.DirectionAt(si[i]), fu.DirectionAt(si[i + 1]), false);
					double angle = Math.Abs(new SweepAngle(fu.DirectionAt(si[i]), fu.DirectionAt(si[i + 1])));
					double dp = si[i + 1] - si[i];
					if (angle < minTan)
					{
						minTan = angle;
						bestPair = i;
					}
				}
				if (bestPair >= 0)
				{
					vMinRestrict = fu.PositionToParameter(si[bestPair]);
					vMaxRestrict = fu.PositionToParameter(si[bestPair + 1]);
				}
				else
				{
					if ((fu.StartPoint | fu.EndPoint) < Precision.eps)
					{
						res.SetPeriodic(res.IsUPeriodic, true);
					}
				}
			}
			if (uMinRestrict != uMaxRestrict || vMinRestrict != vMaxRestrict)
			{
				res.SetPeriodicRestriction(uClosed, vClosed, uMinRestrict, uMaxRestrict, vMinRestrict, vMaxRestrict);
			}
#if DEBUG
			if (res.GetVSingularities().Length == 1)
			{

			}
#endif
			return res;
		}
		public static T ParseEnum<T>(string value)
		{
			if (string.IsNullOrEmpty(value)) value = "Unspecified"; // all enums seem to end with this
			string[] names = Enum.GetNames(typeof(T));
			for (int i = 0; i < names.Length; i++)
			{
				if (names[i].ToUpper() == value.ToUpper()) return (T)Enum.ToObject(typeof(T), i);
			}
			return default(T);
		}
		private GeoObjectList Collect(Item root)
		{
			GeoObjectList res = new GeoObjectList();
			switch (root.type)
			{
				case Item.ItemType.mechanicalDesignGeometricPresentationRepresentation:
					break;
			}
			return res;
		}

		private Item DereferenceItem(Item item)
		{
			if (item.type == Item.ItemType.index) return definitions[(int)item.val];
			return null;
		}

		private void Expect(char expected, string line, int start)
		{
			if (line[start] != expected) throw new SyntaxError(line, start, expected);
		}
		private void AppendDefinition(int index, Item item)
		{   // most files provide an ascending order of indices of definitions
			if (definitions.Count == index) definitions.Add(item);
			else
			{
				while (definitions.Count <= index) definitions.Add(null);
				definitions[index] = item;
			}
		}
		private bool Statement(Dictionary<Item.ItemType, List<int>> roots)
		{
			int start, length;
			string line;
			if (!tk.NextToken(out line, out start, out length)) return tk.EndOfFile;
			if (line[start] == '#') Definition(int.Parse(line.Substring(start + 1, length - 1)), roots);
			else
			{   // den anderen Kram erstmal einfach überlesen
				while (line[start] != ';')
				{
					if (!tk.NextToken(out line, out start, out length)) return false;
				}
			}
			return true;
		}
		private void Definition(int index, Dictionary<Item.ItemType, List<int>> roots)
		{
			int start, length;
			string line;
#if DEBUG
			if (index == 361)
			{ }
#endif
			if (!tk.NextToken(out line, out start, out length)) return;
			Expect('=', line, start);
			if (!tk.NextToken(out line, out start, out length)) return;
			if (line[start] == '(') AppendDefinition(index, UnseperatedList());
			else
			{
				Item entity = Entity(line.Substring(start, length));
				AppendDefinition(index, entity);
				if (roots.TryGetValue(entity.type, out List<int> indices))
				{
					indices.Add(index);
				}
			}
			if (!tk.NextToken(out line, out start, out length)) return;
			Expect(';', line, start);
		}

		private Item Entity(string name)
		{
#if DEBUG
			if (allNames.ContainsKey(name)) allNames[name] = allNames[name] + 1;
			else allNames[name] = 1;
#endif
			int start, length;
			string line;
			if (!tk.NextToken(out line, out start, out length)) return null;
			Expect('(', line, start);
			List<Item> lst = Parameters();
#if DEBUG
			StringBuilder sb = new StringBuilder();

			for (int i = 1; i < lst.Count; i++)
			{
				sb.Append(lst[i].type.ToString());
				if (i < lst.Count - 1) sb.Append(", ");
			}
			HashSet<string> insertInto;
			if (!entityPattern.TryGetValue(name, out insertInto))
			{
				insertInto = new HashSet<string>();
				entityPattern[name] = insertInto;
			}
			insertInto.Add(sb.ToString());
#endif
			return new Item(Item.GetType(name), lst);
		}

		private List<Item> Parameters()
		{
			int start, length;
			string line;
			List<Item> res = new List<Item>();
			do
			{
				if (!tk.NextToken(out line, out start, out length)) return null; // der Wert oder '('
				if (line[start] == '(')
				{
					res.Add(new Item(Item.ItemType.list, Parameters()));
				}
				else if (line[start] == ')') break; // keine Parameter, leere Liste
				else
				{   // hier der Wert
					res.Add(GetItem(line, start, length));
				}
				if (!tk.NextToken(out line, out start, out length)) return null; // ',' oder ')'
				if (line[start] == '(')
				{   // es war ein Name, der noch von einer Parameterliste gefolgt wird
					string name = (string)res[res.Count - 1].val;
					res[res.Count - 1] = new Item(Item.GetType(name), Parameters()); // überschreiben, ein Entity ist eine Liste, beginnend mit Namen, gefolgt von Parametern
					if (!tk.NextToken(out line, out start, out length)) return null; // ',' oder ')'
				}
			}
			while (line[start] == ',');
			if (line[start] != ')') throw new SyntaxError(line, start, ')');
			return res;
		}

		private Item UnseperatedList()
		{
			List<Item> lst = new List<Item>();
			int start, length;
			string line;
			if (!tk.NextToken(out line, out start, out length)) return null;
			while (char.IsLetter(line[start]))
			{
				lst.Add(Entity(line.Substring(start, length)));
				if (!tk.NextToken(out line, out start, out length)) return null; // nächster Name oder ')'
			}
			Expect(')', line, start);
			return new Item(Item.ItemType.list, lst);
		}

		private Item GetItem(string line, int start, int length)
		{
			if (char.IsNumber(line[start]) || line[start] == '.' || line[start] == '+' || line[start] == '-')
			{   // if this is a keyword, which starts with a dot (like .T.) the TryParse will fail
				double floatval;
				if (double.TryParse(line.Substring(start, length), NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out floatval))
				{
					return new Item(Item.ItemType.floatval, floatval);
				}
			}
			switch (line[start])
			{
				case '#':
					int intval;
					if (int.TryParse(line.Substring(start + 1, length - 1), out intval))
					{
						return new Item(Item.ItemType.index, intval);
					}
					break;
				case '\'':
					string stringval = line.Substring(start + 1, length - 2);
					return new Item(Item.ItemType.stringval, stringval);
				case '.':
					string keyword = line.Substring(start + 1, length - 2);
					return new Item(Item.ItemType.keyword, keyword);
				case '$':
					return new Item(Item.ItemType.not_set, null);
				case '*':
					return new Item(Item.ItemType.use_default, null);
			}
			return new Item(Item.ItemType.name, line.Substring(start, length));
		}
	}
	// #endif
}
