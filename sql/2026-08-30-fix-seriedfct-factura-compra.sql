-- Corrige el SerieDfct de "Factura Compra" (CodigoObj 13): apuntaba a la serie 11,
-- que pertenece a Factura de venta (CodigoObj 6). La serie primaria real de
-- CodigoObj 13 es la 27 (la 26 es la serie Manual).
-- Idempotente: solo actualiza si sigue mal.
UPDATE NumeracionDocumento
SET SerieDfct = 27
WHERE CodigoObj = '13' AND (SerieDfct IS NULL OR SerieDfct <> 27);

SELECT CodigoObj, SerieDfct, DocAlias FROM NumeracionDocumento WHERE CodigoObj = '13';
