export interface ParserSection {
    delimiter: string;
    flexCheckHasHeaders: boolean;
    flexCheckSkipEmptyLines: boolean;
    txtQuoteCharacter: string;
    txtEscapeCharacter: string;
    flexCheckOrderByColumnListForDedup : boolean;
    order_by_column_list_name : string;
    order_by_column_list_name_sort_dir : string;
    order_by_column_list_for_dedup: string;    
    is_active : boolean;
    do_not_archive_file : boolean;
    ignore_duplicate_rows : boolean
}