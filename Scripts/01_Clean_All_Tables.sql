-- ==================================================================================
-- SCRIPT: 01_Clean_All_Tables.sql
-- DESCRIPCIÓN: Limpieza segura de todas las tablas existentes para el servidor MySQL.
-- NOTA: No ejecuta DROP DATABASE para preservar la base de datos y usuario del hosting.
-- FECHA: 2026-08-24
-- ==================================================================================

-- 1. Desactivar validación de claves foráneas temporalmente
SET FOREIGN_KEY_CHECKS = 0;

-- 2. Eliminar tablas de seguridad, tokens y sesiones
DROP TABLE IF EXISTS `PasswordResetToken`;
DROP TABLE IF EXISTS `Login`;
DROP TABLE IF EXISTS `RoleAction`;
DROP TABLE IF EXISTS `UserRoleModule`;
DROP TABLE IF EXISTS `Action`;
DROP TABLE IF EXISTS `Operation`;
DROP TABLE IF EXISTS `Module`;
DROP TABLE IF EXISTS `User`;
DROP TABLE IF EXISTS `UserRole`;
DROP TABLE IF EXISTS `IdentificationType`;
DROP TABLE IF EXISTS `PaymentMethod`;

-- 3. Eliminar tablas del negocio de parqueadero
DROP TABLE IF EXISTS `TicketDiscounts`;
DROP TABLE IF EXISTS `CommercialAgreements`;
DROP TABLE IF EXISTS `Stores`;
DROP TABLE IF EXISTS `ParkingTickets`;
DROP TABLE IF EXISTS `WorkShifts`;
DROP TABLE IF EXISTS `MonthlySubscriptions`;
DROP TABLE IF EXISTS `VehicleRates`;

-- 4. Eliminar historial de migraciones de Entity Framework para reseteo completo
DROP TABLE IF EXISTS `__EFMigrationsHistory`;

-- 5. Reactivar validación de claves foráneas
SET FOREIGN_KEY_CHECKS = 1;

SELECT 'Todas las tablas han sido eliminadas exitosamente. La base de datos está lista para la migración DDL pura.' AS Resultado;
